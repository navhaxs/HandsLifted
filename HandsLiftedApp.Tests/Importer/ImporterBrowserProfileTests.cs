using System;
using System.IO;
using System.Threading.Tasks;
using HandsLiftedApp.Importer.OnlineSongLyrics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Importer;

[TestClass]
public class ImporterBrowserProfileTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ImporterBrowserProfileTests_" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private void CreateProfileWithFiles()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "EBWebView", "Default", "Network"));
        File.WriteAllText(Path.Combine(_dir, "EBWebView", "Default", "Network", "Cookies"), "x");
        File.WriteAllText(Path.Combine(_dir, "EBWebView", "Local State"), "x");
    }

    [TestMethod]
    public async Task TryClear_RemovesTheWholeProfileFolder()
    {
        CreateProfileWithFiles();

        var ok = await ImporterBrowserProfile.TryClearAsync(_dir);

        Assert.IsTrue(ok);
        Assert.IsFalse(Directory.Exists(_dir));
    }

    [TestMethod]
    public async Task TryClear_WhenFolderDoesNotExist_Succeeds()
    {
        Assert.IsTrue(await ImporterBrowserProfile.TryClearAsync(_dir));
    }

    [TestMethod]
    public async Task TryClear_WhenAFileIsLocked_ReturnsFalseAndKeepsRetryingThenSucceedsOnceReleased()
    {
        // Only Windows refuses to delete a file another handle has open.
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("File locking semantics differ on this platform.");
        }

        CreateProfileWithFiles();
        var locked =new FileStream(
            Path.Combine(_dir, "EBWebView", "Default", "Network", "Cookies"),
            FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            Assert.IsFalse(await ImporterBrowserProfile.TryClearAsync(_dir, attempts: 2, retryDelayMs: 10));
            Assert.IsTrue(Directory.Exists(_dir));
        }
        finally
        {
            locked.Dispose();
        }

        Assert.IsTrue(await ImporterBrowserProfile.TryClearAsync(_dir, attempts: 2, retryDelayMs: 10));
        Assert.IsFalse(Directory.Exists(_dir));
    }

    [TestMethod]
    public void UserDataFolder_IsInsideTheUsersAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        StringAssert.StartsWith(ImporterBrowserProfile.UserDataFolder, appData);
    }
}
