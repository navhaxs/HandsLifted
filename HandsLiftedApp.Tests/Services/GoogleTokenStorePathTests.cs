using HandsLiftedApp.Importer.GoogleSlides;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class GoogleTokenStorePathTests
{
    private string _root = null!;

    [TestInitialize]
    public void Init()
    {
        _root = Path.Combine(Path.GetTempPath(), "tokenpath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    private string Dir(string name)
    {
        var d = Path.Combine(_root, name);
        Directory.CreateDirectory(d);
        return d;
    }

    [TestMethod]
    public void Resolve_PutsTheTokenInTheDataDirNotTheAppFolder()
    {
        var data = Dir("data");
        var app = Dir("app");

        var path = GoogleTokenStorePath.Resolve(data, app);

        Assert.AreEqual(Path.Combine(data, "token.json"), path);
    }

    [TestMethod]
    public void Resolve_CopiesAnExistingTokenFromTheOldAppFolderLocation()
    {
        var data = Dir("data");
        var app = Dir("app");
        var oldToken = Path.Combine(app, "token.json");
        Directory.CreateDirectory(oldToken);
        File.WriteAllText(Path.Combine(oldToken, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user"), "secret");

        var path = GoogleTokenStorePath.Resolve(data, app);

        Assert.AreEqual("secret", File.ReadAllText(Path.Combine(path, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user")));
        Assert.IsTrue(Directory.Exists(oldToken), "the old token is left in place");
    }

    [TestMethod]
    public void Resolve_DoesNotOverwriteATokenAlreadyInTheDataDir()
    {
        var data = Dir("data");
        var app = Dir("app");
        var newToken = Path.Combine(data, "token.json");
        Directory.CreateDirectory(newToken);
        File.WriteAllText(Path.Combine(newToken, "t"), "new");
        var oldToken = Path.Combine(app, "token.json");
        Directory.CreateDirectory(oldToken);
        File.WriteAllText(Path.Combine(oldToken, "t"), "old");

        GoogleTokenStorePath.Resolve(data, app);

        Assert.AreEqual("new", File.ReadAllText(Path.Combine(newToken, "t")));
    }

    [TestMethod]
    public void Resolve_WithNoOldTokenDoesNotCreateAnything()
    {
        var data = Dir("data");

        var path = GoogleTokenStorePath.Resolve(data, Dir("app"));

        Assert.IsFalse(Directory.Exists(path));
    }
}
