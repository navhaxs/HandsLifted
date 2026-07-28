using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureLocalUsxStore
{
    private static readonly Regex ValidIdentifierPattern = new("^[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, string> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ScriptureLocalUsxStore(string rootPath)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
    }

    public async Task<ScriptureBook> LoadBookAsync(string bookCode)
    {
        if (string.IsNullOrWhiteSpace(bookCode))
        {
            throw new ArgumentException("Book code is required.", nameof(bookCode));
        }

        if (!ValidIdentifierPattern.IsMatch(bookCode))
        {
            throw new ArgumentException(
                $"Book code '{bookCode}' is invalid; only letters, digits, and underscores are allowed.",
                nameof(bookCode));
        }

        var xml = await GetXmlAsync(bookCode).ConfigureAwait(false);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return UsxScriptureParser.Parse(document);
    }

    private async Task<string> GetXmlAsync(string bookCode)
    {
        var normalizedBook = bookCode.Trim().ToLowerInvariant();

        if (_memoryCache.TryGetValue(normalizedBook, out var cached))
        {
            return cached;
        }

        var diskPath = GetDiskPath(normalizedBook);
        if (!File.Exists(diskPath))
        {
            throw new ScriptureBookNotFoundException(normalizedBook, diskPath);
        }

        var xml = await File.ReadAllTextAsync(diskPath).ConfigureAwait(false);
        _memoryCache[normalizedBook] = xml;
        return xml;
    }

    private string GetDiskPath(string normalizedBook) => Path.Combine(_rootPath, $"{normalizedBook}.usx");
}
