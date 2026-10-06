using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Tests;

[TestClass]
public class MediaLibraryQueryViewModelTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaLibraryQueryViewModelTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private MediaLibraryQueryViewModel CreateVm(string? label = "Home")
    {
        var library = new Library(new LibraryConfig.LibraryDefinition
        {
            Label = label,
            Directory = _tempDir,
            Type = LibraryType.Media
        });
        return new MediaLibraryQueryViewModel(library);
    }

    [TestMethod]
    public void Constructor_ListsSubfoldersBeforeFiles()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Sermons"));
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();

        Assert.AreEqual(2, vm.Entries.Count);
        Assert.IsTrue(vm.Entries[0].IsDirectory);
        Assert.AreEqual("Sermons", vm.Entries[0].Title);
        Assert.IsFalse(vm.Entries[1].IsDirectory);
        Assert.AreEqual("photo.jpg", vm.Entries[1].Title);
    }

    [TestMethod]
    public void Constructor_UnsupportedExtension_Excluded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "notes.docx"), "not media");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();

        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("photo.jpg", vm.Entries[0].Title);
    }

    [TestMethod]
    public void NavigateIntoCommand_UpdatesEntriesAndBreadcrumbs()
    {
        var sermonsDir = Path.Combine(_tempDir, "Sermons");
        Directory.CreateDirectory(sermonsDir);
        File.WriteAllText(Path.Combine(sermonsDir, "sermon1.mp4"), "mp4-bytes");

        var vm = CreateVm();
        var sermonsEntry = vm.Entries.Single(e => e.Title == "Sermons");

        vm.NavigateIntoCommand.Execute(sermonsEntry).Subscribe();

        Assert.AreEqual("Sermons", vm.CurrentRelativePath);
        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("sermon1.mp4", vm.Entries[0].Title);
        Assert.AreEqual(2, vm.Breadcrumbs.Count);
        Assert.AreEqual("Home", vm.Breadcrumbs[0].Label);
        Assert.AreEqual("", vm.Breadcrumbs[0].RelativePath);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].Label);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].RelativePath);
    }

    [TestMethod]
    public void NavigateToBreadcrumbCommand_JumpsBackToRoot()
    {
        var sermonsDir = Path.Combine(_tempDir, "Sermons");
        Directory.CreateDirectory(sermonsDir);
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.NavigateIntoCommand.Execute(vm.Entries.Single(e => e.Title == "Sermons")).Subscribe();

        vm.NavigateToBreadcrumbCommand.Execute("").Subscribe();

        Assert.AreEqual("", vm.CurrentRelativePath);
        Assert.AreEqual(2, vm.Entries.Count);
        Assert.AreEqual(1, vm.Breadcrumbs.Count);
    }

    [TestMethod]
    public void SearchTerm_FiltersCurrentFolderOnly_NotRecursively()
    {
        var subDir = Path.Combine(_tempDir, "Sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "apple.jpg"), "jpg-bytes");
        File.WriteAllText(Path.Combine(subDir, "banana.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.SearchTerm = "banana";

        Assert.AreEqual(0, vm.Entries.Count,
            "banana.jpg lives in Sub, not the root folder currently being viewed — search must not recurse into it.");
    }

    [TestMethod]
    public void SearchTerm_MatchesFolderNamesToo()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Sermons"));
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.SearchTerm = "serm";

        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("Sermons", vm.Entries[0].Title);
    }

    [TestMethod]
    public void MissingDirectory_ReturnsEmptyEntries_NoException()
    {
        var library = new Library(new LibraryConfig.LibraryDefinition
        {
            Label = "Home",
            Directory = Path.Combine(_tempDir, "DoesNotExist"),
            Type = LibraryType.Media
        });

        var vm = new MediaLibraryQueryViewModel(library);

        Assert.AreEqual(0, vm.Entries.Count);
    }

    [TestMethod]
    public void NavigateIntoCommand_ResetsSearchTerm()
    {
        var sermonsDir = Path.Combine(_tempDir, "Sermons");
        Directory.CreateDirectory(sermonsDir);
        File.WriteAllText(Path.Combine(sermonsDir, "sermon1.mp4"), "mp4-bytes");
        File.WriteAllText(Path.Combine(_tempDir, "unrelated.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.SearchTerm = "Sermons";
        Assert.AreEqual(1, vm.Entries.Count, "Filter should narrow the root listing to just the Sermons folder.");

        var sermonsEntry = vm.Entries.Single(e => e.Title == "Sermons");
        vm.NavigateIntoCommand.Execute(sermonsEntry).Subscribe();

        Assert.AreEqual("", vm.SearchTerm, "Navigating into a folder must clear the stale filter.");
        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("sermon1.mp4", vm.Entries[0].Title,
            "With the filter cleared, the Sermons folder's own contents must be visible, not filtered out by the old 'Sermons' search term.");
    }

    private MediaLibraryQueryViewModel CreatePickVm(Action<HandsLiftedApp.Core.Models.Library.HomeLibraryEntry> onChosen)
    {
        var library = new Library(new LibraryConfig.LibraryDefinition
        {
            Label = "Motion", Directory = _tempDir, Type = LibraryType.Media
        });
        var videoOnly = new System.Collections.Generic.HashSet<string>(
            new[] { "mp4" }, StringComparer.OrdinalIgnoreCase);
        return new MediaLibraryQueryViewModel(library, videoOnly, onChosen);
    }

    [TestMethod]
    public void ExtensionFilter_LimitsFilesShown_FoldersKept()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Loops"));
        File.WriteAllText(Path.Combine(_tempDir, "clip.mp4"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "x");

        var vm = CreatePickVm(_ => { });

        CollectionAssert.AreEqual(new[] { "Loops", "clip.mp4" }, vm.Entries.Select(e => e.Title).ToArray());
    }

    [TestMethod]
    public void ChooseFile_FileInvokesCallback_FolderDoesNot_AndPickModeFlagSet()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Loops"));
        File.WriteAllText(Path.Combine(_tempDir, "clip.mp4"), "x");
        string? chosen = null;
        var vm = CreatePickVm(e => chosen = e.FullPath);

        vm.ChooseFile(vm.Entries.Single(e => e.IsDirectory));
        Assert.IsNull(chosen);

        vm.ChooseFile(vm.Entries.Single(e => !e.IsDirectory));
        Assert.AreEqual(Path.Combine(_tempDir, "clip.mp4"), chosen);
        Assert.IsTrue(vm.IsPickMode);
        Assert.IsFalse(CreateVm().IsPickMode);
    }

    [TestMethod]
    public void FolderPreviewFinder_PassesThroughNestedFolders_BreadthFirst_Capped()
    {
        var deep = Path.Combine(_tempDir, "A", "B", "C");
        Directory.CreateDirectory(deep);
        Directory.CreateDirectory(Path.Combine(_tempDir, "A", "D"));
        File.WriteAllText(Path.Combine(deep, "deep.mp4"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "A", "top.mp4"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "A", "ignored.txt"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "A", "D", "d1.mp4"), "x");

        var exts = new System.Collections.Generic.HashSet<string>(new[] { "mp4" }, StringComparer.OrdinalIgnoreCase);
        var found = HandsLiftedApp.Core.Models.Library.FolderPreviewFinder.Find(Path.Combine(_tempDir, "A"), exts);

        CollectionAssert.AreEqual(
            new[] { Path.Combine(_tempDir, "A", "top.mp4"), Path.Combine(_tempDir, "A", "D", "d1.mp4"), Path.Combine(deep, "deep.mp4") },
            found.ToArray());
    }

    [TestMethod]
    public void FolderPreviewFinder_SingleSubfolderOfManyClips_FillsAllFourSlots_EvenlySpaced()
    {
        var sub = Path.Combine(_tempDir, "Calvary", "H265");
        Directory.CreateDirectory(sub);
        for (var i = 1; i <= 10; i++) File.WriteAllText(Path.Combine(sub, $"Calvary_{i:00}.mp4"), "x");

        var exts = new System.Collections.Generic.HashSet<string>(new[] { "mp4" }, StringComparer.OrdinalIgnoreCase);
        var found = HandsLiftedApp.Core.Models.Library.FolderPreviewFinder.Find(Path.Combine(_tempDir, "Calvary"), exts);

        CollectionAssert.AreEqual(
            new[] { "Calvary_02.mp4", "Calvary_04.mp4", "Calvary_07.mp4", "Calvary_09.mp4" },
            found.Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void FolderPreviewFinder_EmptyOrMissingFolder_ReturnsNothing()
    {
        var exts = new System.Collections.Generic.HashSet<string>(new[] { "mp4" });
        Assert.AreEqual(0, HandsLiftedApp.Core.Models.Library.FolderPreviewFinder.Find(_tempDir, exts).Count);
        Assert.AreEqual(0, HandsLiftedApp.Core.Models.Library.FolderPreviewFinder.Find(Path.Combine(_tempDir, "nope"), exts).Count);
    }

    [TestMethod]
    public void NavigateIntoCommand_TwoLevelsDeep_AccumulatesBreadcrumbsCorrectly()
    {
        var level1 = Path.Combine(_tempDir, "Sermons");
        var level2 = Path.Combine(level1, "2024");
        Directory.CreateDirectory(level2);
        File.WriteAllText(Path.Combine(level2, "sermon.mp4"), "mp4-bytes");

        var vm = CreateVm();
        vm.NavigateIntoCommand.Execute(vm.Entries.Single(e => e.Title == "Sermons")).Subscribe();
        vm.NavigateIntoCommand.Execute(vm.Entries.Single(e => e.Title == "2024")).Subscribe();

        Assert.AreEqual(Path.Combine("Sermons", "2024"), vm.CurrentRelativePath);
        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("sermon.mp4", vm.Entries[0].Title);

        Assert.AreEqual(3, vm.Breadcrumbs.Count);
        Assert.AreEqual("Home", vm.Breadcrumbs[0].Label);
        Assert.AreEqual("", vm.Breadcrumbs[0].RelativePath);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].Label);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].RelativePath);
        Assert.AreEqual("2024", vm.Breadcrumbs[2].Label);
        Assert.AreEqual(Path.Combine("Sermons", "2024"), vm.Breadcrumbs[2].RelativePath);
    }
}
