using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HandsLiftedApp.Core.Services;

public static class ImportCacheService
{
    public static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VisionScreens", "ImportCache");

    /// <summary>
    /// File extensions a completed conversion leaves in a cache directory: images from the
    /// PDF/PNG rasterization step, plus any embedded videos extracted out of a .pptx in place of a
    /// slide's PNG (see EmbeddedVideoExtractor). Matches the filter the group-item instances apply
    /// when turning a cache directory back into slides
    /// (PowerPointPresentationItemInstance.CollectSlideFilesInOrder).
    /// </summary>
    private static readonly HashSet<string> ExportImageExtensions = BuildExportExtensions();

    private static HashSet<string> BuildExportExtensions()
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        foreach (var videoExt in Constants.SUPPORTED_VIDEO)
        {
            extensions.Add("." + videoExt);
        }
        return extensions;
    }

    /// <summary>
    /// True when <paramref name="targetDirectory"/> already holds rasterized slide exports from a
    /// previous conversion run, so the (expensive) conversion can be skipped and the slides rebuilt
    /// straight from disk. Cache directories keyed by <see cref="GetFileImportCacheDirectory"/> are
    /// keyed on source-file *content*, so a warm directory can only hold output for that exact
    /// content — never a stale render of a since-edited file.
    /// </summary>
    public static bool HasUsableCachedExports(string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return false;
        }

        // Images alone don't prove the conversion finished: a crashed or cancelled run leaves some behind.
        // Only a directory whose conversion ran to the end (and was marked so) can be reused.
        if (!File.Exists(Path.Combine(targetDirectory, CompletionMarkerFileName)))
        {
            return false;
        }

        return Directory.EnumerateFiles(targetDirectory)
            .Any(filePath => ExportImageExtensions.Contains(Path.GetExtension(filePath)));
    }

    /// <summary>Written into a cache directory once a conversion has finished successfully.</summary>
    public const string CompletionMarkerFileName = ".import-complete";

    /// <summary>
    /// Call before a fresh conversion into <paramref name="targetDirectory"/>: clears whatever an earlier,
    /// unfinished run left (and its completion marker) so nothing stale is mixed into the new export.
    /// </summary>
    public static void BeginImport(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(targetDirectory))
        {
            try { File.Delete(file); }
            catch (IOException) { /* in use: it will be overwritten by the new export */ }
        }
    }

    /// <summary>Records that the conversion into <paramref name="targetDirectory"/> finished, so it may be reused.</summary>
    public static void MarkImportComplete(string targetDirectory)
    {
        File.WriteAllText(Path.Combine(targetDirectory, CompletionMarkerFileName), DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// A sub-directory of <paramref name="parentDirectory"/> named after the content of <paramref name="contentFile"/>,
    /// for sources that have no content-keyed directory of their own (Google Slides). Changed content gets a new
    /// directory, so image paths never repeat for different pictures.
    /// </summary>
    public static string GetContentSubdirectory(string parentDirectory, string contentFile, string prefix)
    {
        using var stream = File.OpenRead(contentFile);
        var hash = Convert.ToHexString(SHA256.HashData(stream))[..16];
        var dir = Path.Combine(parentDirectory, prefix + hash);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Cache dir for a local file. Stable per file content, so it survives the source file moving to a different absolute path (e.g. a different machine).</summary>
    public static string GetFileImportCacheDirectory(string sourceFilePath)
    {
        using var stream = File.OpenRead(sourceFilePath);
        var hash = SHA256.HashData(stream);
        return GetOrCreateDir(Convert.ToHexString(hash)[..16]);
    }

    /// <summary>Cache dir for a non-file source (e.g. Google Slides presentation ID).</summary>
    public static string GetKeyedCacheDirectory(string keyType, string key)
    {
        return GetOrCreateDir(HashKey($"{keyType}|{key}"));
    }

    private static string GetOrCreateDir(string hash)
    {
        var dir = Path.Combine(CacheRoot, hash);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..16];
    }
}
