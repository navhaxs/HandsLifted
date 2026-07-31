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
    /// Image extensions a completed conversion leaves in a cache directory. Matches the filter
    /// the group-item instances apply when turning a cache directory back into slides.
    /// </summary>
    private static readonly HashSet<string> ExportImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

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

        return Directory.EnumerateFiles(targetDirectory)
            .Any(filePath => ExportImageExtensions.Contains(Path.GetExtension(filePath)));
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
