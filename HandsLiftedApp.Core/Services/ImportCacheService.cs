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

    /// <summary>Cache dir for a local file. Stable per absolute path.</summary>
    public static string GetFileImportCacheDirectory(string sourceFilePath)
    {
        var key = Path.GetFullPath(sourceFilePath);
        return GetOrCreateDir(HashKey(key));
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
