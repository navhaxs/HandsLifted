using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Importer.Scripture;

[TestClass]
public class ScriptureTranslationCatalogTests
{
    // A trimmed real-shape fixture covering every license shape actually seen in fetch.bible's
    // manifest.json: a bare "public" string, an object with a "license" field (cc-by-sa, not
    // public domain), an object with only forbid_* flags and no "license" field at all (Lexham
    // English Bible's real shape - commercially licensed despite appearing in this catalog), and
    // a non-English entry that must be excluded regardless of its license.
    private const string ManifestJson = """
        {
          "bibles": {
            "eng_bsb": {
              "name": { "english": "Berean Standard Bible", "english_abbrev": "BSB" },
              "copyright": { "licenses": ["public"] }
            },
            "eng_ulb": {
              "name": { "english": "Unlocked Literal Bible", "english_abbrev": "ULB" },
              "copyright": { "licenses": [{ "license": "cc-by-sa", "url": "https://example.com" }] }
            },
            "eng_leb": {
              "name": { "english": "Lexham English Bible", "english_abbrev": "LEB" },
              "copyright": { "licenses": [{ "forbid_commercial": true, "forbid_derivatives": true }] }
            },
            "fra_lsg": {
              "name": { "english": "Louis Segond", "english_abbrev": "LSG" },
              "copyright": { "licenses": ["public"] }
            }
          }
        }
        """;

    [TestMethod]
    public async Task GetPublicDomainEnglishTranslationsAsync_FiltersToEnglishPublicDomainOnly()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ManifestJson) });
        var catalog = new ScriptureTranslationCatalog(new HttpClient(handler));

        var translations = await catalog.GetPublicDomainEnglishTranslationsAsync();

        Assert.AreEqual(1, translations.Count);
        Assert.AreEqual("eng_bsb", translations[0].Code);
        Assert.AreEqual("Berean Standard Bible", translations[0].Name);
        Assert.AreEqual("BSB", translations[0].Abbrev);
    }
}
