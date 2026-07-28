using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureSourceLoader
{
    private const string BaseUrl = "https://v1.fetch.bible/bibles/";

    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;
    private readonly ConcurrentDictionary<string, string> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ScriptureSourceLoader(HttpClient? httpClient = null, string? cacheRoot = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HandsLifted", "ScriptureCache");
    }

    public async Task<ScriptureBook> LoadBookAsync(string translation, string bookCode)
    {
        if (string.IsNullOrWhiteSpace(translation))
        {
            throw new ArgumentException("Translation is required.", nameof(translation));
        }

        if (string.IsNullOrWhiteSpace(bookCode))
        {
            throw new ArgumentException("Book code is required.", nameof(bookCode));
        }

        var xml = await GetXmlAsync(translation, bookCode).ConfigureAwait(false);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return UsxScriptureParser.Parse(document);
    }

    private async Task<string> GetXmlAsync(string translation, string bookCode)
    {
        var normalizedTranslation = translation.Trim().ToLowerInvariant();
        var normalizedBook = bookCode.Trim().ToLowerInvariant();
        var key = $"{normalizedTranslation}/{normalizedBook}";

        if (_memoryCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var diskPath = GetDiskCachePath(normalizedTranslation, normalizedBook);
        if (File.Exists(diskPath))
        {
            var diskXml = await File.ReadAllTextAsync(diskPath).ConfigureAwait(false);
            _memoryCache[key] = diskXml;
            return diskXml;
        }

        var uri = new Uri($"{BaseUrl}{normalizedTranslation}/usx/{normalizedBook}.usx", UriKind.Absolute);
        using var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Scripture fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}) for {key}.");
        }

        var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        await WriteToDiskCacheAsync(normalizedTranslation, normalizedBook, xml).ConfigureAwait(false);
        _memoryCache[key] = xml;

        return xml;
    }

    private string GetDiskCachePath(string normalizedTranslation, string normalizedBook) =>
        Path.Combine(_cacheRoot, normalizedTranslation, $"{normalizedBook}.usx");

    private async Task WriteToDiskCacheAsync(string normalizedTranslation, string normalizedBook, string xml)
    {
        var finalPath = GetDiskCachePath(normalizedTranslation, normalizedBook);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        var tempPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tempPath, xml).ConfigureAwait(false);
        File.Move(tempPath, finalPath, overwrite: true);
    }
}
