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
}
