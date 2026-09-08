using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using HandsLiftedApp.Importer.PowerPointLib;
using Serilog;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Resolves and downloads PowerPoint slides' externally-linked (TargetMode="External") video
/// references that point at a hosted video service (e.g. a Vimeo embed URL) via yt-dlp, so they play
/// back exactly like a locally-embedded video with no live network dependency afterward. Requires
/// yt-dlp on the system PATH — not bundled. Never throws: every failure degrades to a warning message
/// and the slide's existing static PNG stands in, matching EmbeddedVideoExtractor's contract.
/// </summary>
public static class ExternalVideoDownloader
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public static void DownloadPendingVideos(
        IReadOnlyList<PendingExternalVideo> pendingVideos,
        int shownSlideCount,
        string outputDirectory,
        List<string> warnings)
    {
        foreach (var pending in pendingVideos)
        {
            DownloadOne(pending, shownSlideCount, outputDirectory, warnings);
        }
    }

    internal static string PadSlideNumber(int slideNumber, int shownSlideCount)
    {
        int maxDigits = (int)Math.Floor(Math.Log10(shownSlideCount) + 1);
        return slideNumber.ToString(new string('0', maxDigits));
    }

    internal static bool AlreadyDownloaded(string outputDirectory, string paddedNumber)
    {
        var prefix = $"Slide.{paddedNumber}.";
        return Directory.GetFiles(outputDirectory).Any(f =>
            Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            SupportedExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()));
    }

    internal static IReadOnlyList<string> BuildArguments(string url, string outputPathTemplate) => new[]
    {
        "-f", "best[height<=1080]/bestvideo[height<=1080]+bestaudio",
        "--merge-output-format", "mp4",
        "-o", outputPathTemplate,
        "--no-playlist",
        "--newline",
        url,
    };

    private static void DownloadOne(PendingExternalVideo pending, int shownSlideCount, string outputDirectory, List<string> warnings)
    {
        var paddedNumber = PadSlideNumber(pending.SlideNumber, shownSlideCount);

        if (AlreadyDownloaded(outputDirectory, paddedNumber))
        {
            Log.Debug("Slide {SlideNumber}: external video already downloaded, skipping", pending.SlideNumber);
            return;
        }

        var outputTemplate = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.%(ext)s");
        var startInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in BuildArguments(pending.Url, outputTemplate))
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception)
        {
            process = null;
        }

        if (process == null)
        {
            warnings.Add($"Slide {pending.SlideNumber}: needs yt-dlp to import this video — install it (see https://github.com/yt-dlp/yt-dlp#installation) and re-sync.");
            return;
        }

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) stderr.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        bool exited = process.WaitForExit((int)DownloadTimeout.TotalMilliseconds);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            warnings.Add($"Slide {pending.SlideNumber}: video download timed out after {DownloadTimeout.TotalMinutes:0} minutes.");
            return;
        }

        if (process.ExitCode != 0)
        {
            var detail = LastNonEmptyLine(stderr.ToString());
            warnings.Add(detail != null
                ? $"Slide {pending.SlideNumber}: video download failed — {detail}"
                : $"Slide {pending.SlideNumber}: video download failed.");
            return;
        }

        var downloadedPath = FindNonImageFileWithPrefix(outputDirectory, paddedNumber);
        if (downloadedPath == null)
        {
            warnings.Add($"Slide {pending.SlideNumber}: video download reported success but no output file was found.");
            return;
        }

        var ext = Path.GetExtension(downloadedPath).TrimStart('.').ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
        {
            warnings.Add($"Slide {pending.SlideNumber}: downloaded video format '.{ext}' isn't supported.");
            File.Delete(downloadedPath);
            return;
        }

        var siblingPng = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.png");
        if (File.Exists(siblingPng)) File.Delete(siblingPng);

        Log.Debug("Slide {SlideNumber}: downloaded external video to {Path}", pending.SlideNumber, downloadedPath);
    }

    // Excludes only image extensions (rather than requiring a supported video extension) so that a
    // download producing an unexpected-but-real format still gets found and reported with the
    // specific "unsupported format" warning, instead of the vaguer "no output file was found" one.
    private static string? FindNonImageFileWithPrefix(string outputDirectory, string paddedNumber)
    {
        var prefix = $"Slide.{paddedNumber}.";
        return Directory.GetFiles(outputDirectory)
            .FirstOrDefault(f =>
                Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !ImageExtensions.Contains(Path.GetExtension(f)));
    }

    private static string? LastNonEmptyLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : null;
    }
}
