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
        new(Constants.SUPPORTED_VIDEO, StringComparer.OrdinalIgnoreCase);

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
        var stem = $"Slide.{paddedNumber}";
        return Directory.GetFiles(outputDirectory).Any(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase) &&
            SupportedExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()));
    }

    internal static IReadOnlyList<string> BuildArguments(string url, string outputPathTemplate) => new[]
    {
        // Cap at 1080p, preferring a single already-combined stream so ffmpeg (needed to merge
        // separate video+audio streams) isn't required for most sites; falls back to
        // bestvideo+bestaudio (which does need ffmpeg to merge) only when no combined stream exists.
        "-f", "best[height<=1080]/bestvideo[height<=1080]+bestaudio",
        "--merge-output-format", "mp4",
        "-o", outputPathTemplate,
        "--no-playlist",
        "--newline",
        "--ignore-config",
        "--socket-timeout", "20",
        "--retries", "2",
        url,
    };

    private static void DownloadOne(PendingExternalVideo pending, int shownSlideCount, string outputDirectory, List<string> warnings)
    {
        try
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

            try
            {
                var stderr = new StringBuilder();
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) stderr.AppendLine(e.Data); };
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                bool exited = process.WaitForExit((int)DownloadTimeout.TotalMilliseconds);
                if (!exited)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception) { }
                    DeleteLeftoverArtifacts(outputDirectory, paddedNumber);
                    warnings.Add($"Slide {pending.SlideNumber}: video download timed out after {DownloadTimeout.TotalMinutes:0} minutes.");
                    return;
                }

                // The timed overload doesn't guarantee the async output/error readers have finished
                // draining; the parameterless overload does. Without this, the captured stderr used
                // for the failure message below can be missing its last (often most useful) line.
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var detail = LastNonEmptyLine(stderr.ToString());
                    if (detail != null && detail.Length > 200) detail = detail[..200] + "…";
                    DeleteLeftoverArtifacts(outputDirectory, paddedNumber);
                    warnings.Add(detail != null
                        ? $"Slide {pending.SlideNumber}: video download failed — {detail}"
                        : $"Slide {pending.SlideNumber}: video download failed.");
                    return;
                }

                var downloadedPath = FindDownloadedVideoFile(outputDirectory, paddedNumber);
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
            finally
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch (Exception) { }
                process.Dispose();
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Unexpected error downloading external video for slide {SlideNumber}", pending.SlideNumber);
            warnings.Add($"Slide {pending.SlideNumber}: video download failed unexpectedly.");
        }
    }

    // Matches the exact "Slide.{paddedNumber}" stem (not merely a shared prefix), so an intermediate
    // file yt-dlp leaves behind mid-merge (e.g. "Slide.03.f271.mp4" from the bestvideo+bestaudio
    // fallback) is never mistaken for the final merged output - only its exact-stem filename is.
    private static string? FindDownloadedVideoFile(string outputDirectory, string paddedNumber)
    {
        var stem = $"Slide.{paddedNumber}";
        return Directory.GetFiles(outputDirectory)
            .FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase) &&
                !ImageExtensions.Contains(Path.GetExtension(f)));
    }

    // Removes any leftover non-image files for this slide (e.g. yt-dlp's partial merge intermediates)
    // after a failed or timed-out attempt, so the next sync starts clean rather than picking up a
    // stale intermediate as a slide (CollectSlideFilesInOrder accepts any supported video extension
    // regardless of exact naming) or as an "already downloaded" false positive.
    private static void DeleteLeftoverArtifacts(string outputDirectory, string paddedNumber)
    {
        var prefix = $"Slide.{paddedNumber}.";
        foreach (var file in Directory.GetFiles(outputDirectory)
                     .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                                 !ImageExtensions.Contains(Path.GetExtension(f))))
        {
            try { File.Delete(file); } catch (Exception) { }
        }
    }

    private static string? LastNonEmptyLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : null;
    }
}
