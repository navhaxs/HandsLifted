using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class SlideThemeLibraryTests
{
    private string _root = null!;
    private string _dir = null!;
    private string _media = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "SlideThemeLibraryTests_" + Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(_root, "themes");
        _media = Path.Combine(_root, "media");
        Directory.CreateDirectory(_media);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SlideThemeLibrary NewLibrary() => new(() => _media, a => a());

    private SlideThemeLibrary NewInitialized(BaseSlideTheme? builtIn = null)
    {
        var lib = NewLibrary();
        lib.Initialize(_dir, builtIn);
        return lib;
    }

    private string[] ThemeFiles() => Directory.Exists(_dir)
        ? Directory.GetFiles(_dir, "*.xml").Select(f => Path.GetFileName(f)!).OrderBy(n => n).ToArray()
        : Array.Empty<string>();

    [TestMethod]
    public void Initialize_CreatesFolder_AndPutsBuiltInFirst()
    {
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewInitialized(builtIn);

        Assert.IsTrue(Directory.Exists(_dir));
        Assert.AreSame(builtIn, lib.Themes[0]);
        Assert.AreEqual(1, lib.Themes.Count);
    }

    [TestMethod]
    public void AddedTheme_IsWrittenAndReloadedWithSameId()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Sunday" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        CollectionAssert.AreEqual(new[] { "Sunday.xml" }, ThemeFiles());

        var lib2 = NewInitialized();
        var loaded = lib2.Themes.Single();
        Assert.AreEqual(theme.Id, loaded.Id);
        Assert.AreEqual("Sunday", loaded.Name);
    }

    [TestMethod]
    public void BuiltIn_IsNeverWritten_AndRemovingItDoesNotTouchDisk()
    {
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewInitialized(builtIn);

        lib.SaveNow(builtIn.Id);
        lib.Themes.Remove(builtIn);

        Assert.AreEqual(0, ThemeFiles().Length);
    }

    [TestMethod]
    public void SameNameTwice_SecondGetsNumericSuffix()
    {
        var lib = NewInitialized();
        var a = new BaseSlideTheme { Name = "Dup" };
        var b = new BaseSlideTheme { Name = "Dup" };
        lib.Themes.Add(a);
        lib.Themes.Add(b);
        lib.SaveNow(a.Id);
        lib.SaveNow(b.Id);

        CollectionAssert.AreEqual(new[] { "Dup (2).xml", "Dup.xml" }, ThemeFiles());
    }

    [TestMethod]
    public void Rename_MovesFile()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Old" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        theme.Name = "New";
        lib.SaveNow(theme.Id);

        CollectionAssert.AreEqual(new[] { "New.xml" }, ThemeFiles());
    }

    [TestMethod]
    public void Remove_DeletesFile()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Gone" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        lib.Themes.Remove(theme);

        Assert.AreEqual(0, ThemeFiles().Length);
    }

    [TestMethod]
    public void SaveNow_SkipsWriteWhenContentUnchanged()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Stable" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        var path = Path.Combine(_dir, "Stable.xml");

        File.WriteAllText(path, "SENTINEL");
        lib.SaveNow(theme.Id);

        Assert.AreEqual("SENTINEL", File.ReadAllText(path));
    }

    [TestMethod]
    public void FlushPendingSaves_PersistsEdits()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Edit" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        theme.FontSize = theme.FontSize + 7;
        lib.FlushPendingSaves();

        var loaded = NewInitialized().Themes.Single();
        Assert.AreEqual(theme.FontSize, loaded.FontSize);
    }

    [TestMethod]
    public void CorruptFile_IsSkipped_ValidFileStillLoads()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Good" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        File.WriteAllText(Path.Combine(_dir, "Bad.xml"), "this is not xml");

        var lib2 = NewInitialized();

        Assert.AreEqual(theme.Id, lib2.Themes.Single().Id);
    }

    [TestMethod]
    public void DuplicateIdAcrossFiles_FirstByFilenameWins()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Original" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        File.Copy(Path.Combine(_dir, "Original.xml"), Path.Combine(_dir, "Zcopy.xml"));

        var lib2 = NewInitialized();

        Assert.AreEqual(1, lib2.Themes.Count);
    }

    [TestMethod]
    public void AssetUnderMediaLibrary_StoredRelative_LoadedAbsolute()
    {
        var bg = Path.Combine(_media, "Images", "bg.png");
        Directory.CreateDirectory(Path.GetDirectoryName(bg)!);
        File.WriteAllBytes(bg, new byte[] { 1 });
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Assets", BackgroundGraphicFilePath = bg };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);

        var xml = File.ReadAllText(Path.Combine(_dir, "Assets.xml"));
        Assert.IsFalse(xml.Contains(_media, StringComparison.OrdinalIgnoreCase), "stored path must be relative");
        Assert.AreEqual(bg, NewInitialized().Themes.Single().BackgroundGraphicFilePath);
        Assert.AreEqual(bg, theme.BackgroundGraphicFilePath, "in-memory theme must keep its absolute path");
    }

    [TestMethod]
    public void AssetOutsideMediaLibrary_AndAvares_AreLeftAlone()
    {
        var outside = Path.Combine(_root, "elsewhere", "bg.png");
        var lib = NewInitialized();
        var a = new BaseSlideTheme { Name = "Outside", BackgroundGraphicFilePath = outside };
        var b = new BaseSlideTheme
        {
            Name = "Avares",
            BackgroundGraphicFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/default-bg.png"
        };
        lib.Themes.Add(a);
        lib.Themes.Add(b);
        lib.SaveNow(a.Id);
        lib.SaveNow(b.Id);

        var loaded = NewInitialized().Themes.ToDictionary(t => t.Name);
        Assert.AreEqual(outside, loaded["Outside"].BackgroundGraphicFilePath);
        Assert.AreEqual(b.BackgroundGraphicFilePath, loaded["Avares"].BackgroundGraphicFilePath);
    }

    [TestMethod]
    public void UnusableFolder_DoesNotThrow_AndLeavesBuiltInOnly()
    {
        Directory.CreateDirectory(_root);
        var asFile = Path.Combine(_root, "iam-a-file");
        File.WriteAllText(asFile, "x");
        var builtIn = new BaseSlideTheme { Name = "Built-in" };
        var lib = NewLibrary();

        lib.Initialize(asFile, builtIn);

        Assert.IsNull(lib.Folder);
        Assert.AreSame(builtIn, lib.Themes.Single());
        var extra = new BaseSlideTheme { Name = "Nowhere" };
        lib.Themes.Add(extra);
        lib.SaveNow(extra.Id); // must not throw
    }

    [TestMethod]
    public void InvalidFilenameCharsAndEmptyName_StillProduceValidFiles()
    {
        var lib = NewInitialized();
        var weird = new BaseSlideTheme { Name = "a/b:c?" };
        var empty = new BaseSlideTheme { Name = "" };
        lib.Themes.Add(weird);
        lib.Themes.Add(empty);

        lib.SaveNow(weird.Id);
        lib.SaveNow(empty.Id);

        Assert.AreEqual(2, ThemeFiles().Length);
        Assert.AreEqual(2, NewInitialized().Themes.Count);
    }

    [TestMethod]
    public void Contains_ReflectsThemes()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "C" };
        lib.Themes.Add(theme);

        Assert.IsTrue(lib.Contains(theme.Id));
        Assert.IsFalse(lib.Contains(Guid.NewGuid()));
    }

    [TestMethod]
    public void Initialize_SecondCall_FlushesPendingEditsToOldFolderBeforeSwitching()
    {
        var lib = NewInitialized();
        var theme = new BaseSlideTheme { Name = "Switch" };
        lib.Themes.Add(theme);
        lib.SaveNow(theme.Id);
        theme.FontSize = theme.FontSize + 3;

        var otherDir = Path.Combine(_root, "other");
        lib.Initialize(otherDir, null);

        Assert.AreEqual(0, lib.Themes.Count, "new folder is empty");
        var reloadedOld = NewInitialized().Themes.Single();
        Assert.AreEqual(theme.FontSize, reloadedOld.FontSize, "pending edit must land in the old folder");
    }
}
