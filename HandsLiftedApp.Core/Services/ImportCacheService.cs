using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HandsLiftedApp.Core.Services;

public static class ImportCacheService
{
    public static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VisionScreens", "ImportCache");

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
