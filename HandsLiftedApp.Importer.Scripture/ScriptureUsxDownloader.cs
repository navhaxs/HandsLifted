using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureUsxDownloader
{
    public const string FixedTranslation = "eng_bsb";

    private const string BaseUrl = "https://v1.fetch.bible/bibles/";

    public static readonly IReadOnlyList<string> AllBookCodes = new[]
    {
        "gen", "exo", "lev", "num", "deu", "jos", "jdg", "rut", "1sa", "2sa",
        "1ki", "2ki", "1ch", "2ch", "ezr", "neh", "est", "job", "psa", "pro",
        "ecc", "sng", "isa", "jer", "lam", "ezk", "dan", "hos", "jol", "amo",
        "oba", "jon", "mic", "nam", "hab", "zep", "hag", "zec", "mal",
        "mat", "mrk", "luk", "jhn", "act", "rom", "1co", "2co", "gal", "eph",
        "php", "col", "1th", "2th", "1ti", "2ti", "tit", "phm", "heb", "jas",
        "1pe", "2pe", "1jn", "2jn", "3jn", "jud", "rev"
    };

    private readonly HttpClient _httpClient;

    public ScriptureUsxDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAllBooksAsync(string rootPath, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var total = AllBookCodes.Count;
        var done = 0;

        foreach (var bookCode in AllBookCodes)
        {
            ct.ThrowIfCancellationRequested();

            var destPath = Path.Combine(rootPath, $"{bookCode}.usx");
            if (!File.Exists(destPath))
            {
                try
                {
                    await DownloadOneBookAsync(bookCode, destPath, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to download scripture book {BookCode}", bookCode);
                }
            }

            done++;
            progress?.Report((done, total));
        }
    }

    private async Task DownloadOneBookAsync(string bookCode, string destPath, CancellationToken ct)
    {
        var uri = new Uri($"{BaseUrl}{FixedTranslation}/usx/{bookCode}.usx", UriKind.Absolute);
        using var response = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Scripture fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}) for {bookCode}.");
        }

        var xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        var tempPath = destPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tempPath, xml, ct).ConfigureAwait(false);
        File.Move(tempPath, destPath, overwrite: true);
    }
}
