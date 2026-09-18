using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Importer.Scripture;

public sealed class ScriptureTranslationCatalog
{
    private const string ManifestUrl = "https://v1.fetch.bible/manifest.json";

    private readonly HttpClient _httpClient;

    public ScriptureTranslationCatalog(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public sealed record Translation(string Code, string Name, string Abbrev);

    public async Task<IReadOnlyList<Translation>> GetPublicDomainEnglishTranslationsAsync(CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(ManifestUrl, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync<ManifestRoot>(cancellationToken: ct).ConfigureAwait(false);
        return Filter(manifest);
    }

    internal static IReadOnlyList<Translation> Filter(ManifestRoot? manifest)
    {
        if (manifest?.Bibles == null)
        {
            return Array.Empty<Translation>();
        }

        var result = new List<Translation>();
        foreach (var (code, entry) in manifest.Bibles)
        {
            if (!code.StartsWith("eng_", StringComparison.Ordinal)) continue;
            if (entry.Name?.English is not { Length: > 0 } name) continue;
            if (!IsPublicDomain(entry.Copyright?.Licenses)) continue;

            result.Add(new Translation(code, name, entry.Name.EnglishAbbrev ?? name));
        }

        return result.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Each manifest license entry is either a bare string (e.g. "public", "cc-by-sa") or an
    // object with a "license" string field - but a few entries (Lexham English Bible, NET Bible)
    // instead carry only forbid_* restriction flags with no "license" name at all. Treating
    // anything that isn't an explicit "public" as non-public-domain is both the safe default and
    // happens to be correct for those two specific entries, which are commercially licensed
    // despite appearing in this catalog.
    private static bool IsPublicDomain(List<JsonElement>? licenses)
    {
        if (licenses == null) return false;

        foreach (var license in licenses)
        {
            if (license.ValueKind == JsonValueKind.String && license.GetString() == "public")
            {
                return true;
            }

            if (license.ValueKind == JsonValueKind.Object &&
                license.TryGetProperty("license", out var licenseProp) &&
                licenseProp.ValueKind == JsonValueKind.String &&
                licenseProp.GetString() == "public")
            {
                return true;
            }
        }

        return false;
    }

    internal sealed class ManifestRoot
    {
        [JsonPropertyName("bibles")]
        public Dictionary<string, BibleEntry>? Bibles { get; set; }
    }

    internal sealed class BibleEntry
    {
        [JsonPropertyName("name")]
        public NameInfo? Name { get; set; }

        [JsonPropertyName("copyright")]
        public CopyrightInfo? Copyright { get; set; }
    }

    internal sealed class NameInfo
    {
        [JsonPropertyName("english")]
        public string? English { get; set; }

        [JsonPropertyName("english_abbrev")]
        public string? EnglishAbbrev { get; set; }
    }

    internal sealed class CopyrightInfo
    {
        [JsonPropertyName("licenses")]
        public List<JsonElement>? Licenses { get; set; }
    }
}
