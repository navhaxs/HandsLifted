using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideThemeMigrationTests
{
    private string _root = null!;
    private string _playlistDir = null!;
    private string _media = null!;
    private SlideThemeLibrary _lib = null!;
    private AppPreferencesViewModel _prefs = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "SlideThemeMigrationTests_" + Guid.NewGuid().ToString("N"));
        _playlistDir = Path.Combine(_root, "playlist");
        _media = Path.Combine(_root, "media");
        Directory.CreateDirectory(_playlistDir);
        Directory.CreateDirectory(_media);
        _prefs = new AppPreferencesViewModel();
        Globals.Instance.AppPreferences = _prefs;
        _lib = new SlideThemeLibrary(() => _media, a => a());
        _lib.Initialize(Path.Combine(_root, "themes"), null);
    }

    [TestCleanup]
    public void Teardown()
    {
        _lib?.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SlideThemeMigrationResult Run(BaseSlideTheme[] themes, string? media = "", Guid? song = null,
        Guid? motion = null, Guid? scripture = null) =>
        SlideThemeMigration.Migrate(themes, _playlistDir, media == "" ? _media : media, _lib, _prefs,
            song, motion, scripture);

    [TestMethod]
    public void NewThemes_AreImportedWithTheirIdsPreserved()
    {
        var a = new BaseSlideTheme { Name = "A" };
        var b = new BaseSlideTheme { Name = "B" };

        var result = Run(new[] { a, b });

        Assert.AreEqual(2, result.Imported);
        Assert.IsTrue(_lib.Contains(a.Id));
        Assert.IsTrue(_lib.Contains(b.Id));
        Assert.IsTrue(result.NeedsResave);
    }

    [TestMethod]
    public void ExistingId_IsNotOverwritten_AndReopenImportsNothing()
    {
        var original = new BaseSlideTheme { Name = "Library version" };
        _lib.Themes.Add(original);
        var embedded = new BaseSlideTheme { Name = "Playlist version" };
        embedded.Id = original.Id;

        var first = Run(new[] { embedded });
        var second = Run(new[] { embedded });

        Assert.AreEqual(0, first.Imported);
        Assert.AreEqual(1, first.SkippedExisting);
        Assert.AreEqual(0, second.Imported);
        Assert.AreEqual("Library version", _lib.Themes.Single(t => t.Id == original.Id).Name);
    }

    [TestMethod]
    public void NoEmbeddedThemes_NothingToDo()
    {
        var result = Run(Array.Empty<BaseSlideTheme>());

        Assert.AreEqual(0, result.Imported);
        Assert.IsFalse(result.NeedsResave);
    }

    [TestMethod]
    public void BackgroundImage_IsCopiedIntoMediaLibrary()
    {
        var img = Path.Combine(_playlistDir, "Themes", "Backgrounds", "bg.png");
        Directory.CreateDirectory(Path.GetDirectoryName(img)!);
        File.WriteAllBytes(img, new byte[] { 1, 2, 3 });
        var theme = new BaseSlideTheme
        {
            Name = "Img",
            BackgroundGraphicFilePath = Path.Combine("Themes", "Backgrounds", "bg.png")
        };

        Run(new[] { theme });

        var imported = _lib.Themes.Single();
        StringAssert.StartsWith(imported.BackgroundGraphicFilePath, _media);
        Assert.IsTrue(File.Exists(imported.BackgroundGraphicFilePath));
    }

    [TestMethod]
    public void MediaLibraryNotConfigured_StillImports_KeepingAbsolutePath()
    {
        var img = Path.Combine(_playlistDir, "bg.png");
        File.WriteAllBytes(img, new byte[] { 1 });
        var theme = new BaseSlideTheme { Name = "NoMedia", BackgroundGraphicFilePath = "bg.png" };

        var result = Run(new[] { theme }, media: null);

        Assert.AreEqual(1, result.Imported);
        Assert.AreEqual(img, _lib.Themes.Single().BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void MissingAssetFile_StillImportsTheme()
    {
        var theme = new BaseSlideTheme { Name = "Missing", BackgroundGraphicFilePath = "does-not-exist.png" };

        var result = Run(new[] { theme });

        Assert.AreEqual(1, result.Imported);
        Assert.IsTrue(_lib.Contains(theme.Id));
    }

    [TestMethod]
    public void AvaresBackground_IsLeftUntouched()
    {
        const string avares = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/default-bg.png";
        var theme = new BaseSlideTheme { Name = "Av", BackgroundGraphicFilePath = avares };

        Run(new[] { theme });

        Assert.AreEqual(avares, _lib.Themes.Single().BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void PlaylistDefault_IsAdopted_WhenAppDefaultUnset_AndPlaylistOverrideCleared()
    {
        var theme = new BaseSlideTheme { Name = "D" };

        var result = Run(new[] { theme }, song: theme.Id);

        Assert.AreEqual(theme.Id, _prefs.DefaultSongThemeId);
        Assert.IsNull(result.DefaultSongThemeId);
        Assert.IsTrue(result.AppDefaultsChanged);
    }

    [TestMethod]
    public void PlaylistDefault_DifferingFromSetAppDefault_IsKeptAsOverride()
    {
        var appDefault = new BaseSlideTheme { Name = "App" };
        _lib.Themes.Add(appDefault);
        _prefs.DefaultSongThemeId = appDefault.Id;
        var theme = new BaseSlideTheme { Name = "Mine" };

        var result = Run(new[] { theme }, song: theme.Id);

        Assert.AreEqual(appDefault.Id, _prefs.DefaultSongThemeId);
        Assert.AreEqual(theme.Id, result.DefaultSongThemeId);
        Assert.IsFalse(result.AppDefaultsChanged);
    }

    [TestMethod]
    public void PlaylistDefault_EqualToAppDefault_IsCleared()
    {
        var appDefault = new BaseSlideTheme { Name = "App" };
        _lib.Themes.Add(appDefault);
        _prefs.DefaultScriptureThemeId = appDefault.Id;

        var result = Run(Array.Empty<BaseSlideTheme>(), scripture: appDefault.Id);

        Assert.IsNull(result.DefaultScriptureThemeId);
        Assert.IsTrue(result.NeedsResave);
    }

    [TestMethod]
    public void PlaylistDefault_PointingAtUnknownTheme_IsLeftAlone()
    {
        var dangling = Guid.NewGuid();

        var result = Run(Array.Empty<BaseSlideTheme>(), motion: dangling);

        Assert.AreEqual(dangling, result.DefaultSongMotionThemeId);
        Assert.IsNull(_prefs.DefaultSongMotionThemeId);
        Assert.IsFalse(result.NeedsResave);
    }
}
