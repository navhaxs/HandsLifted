using System;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class GroupSlideActionsTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GroupSlideActionsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static MediaGroupItem.MediaItem Media(string path) => new() { SourceMediaFilePath = path };

    private static MediaGroupItemInstance Group(PlaylistInstance p, string title, params string[] paths)
    {
        var g = new MediaGroupItemInstance(p) { Title = title };
        foreach (var path in paths) g.Items.Add(Media(path));
        p.Items.Add(g);
        return g;
    }

    /// <summary>The media paths in order, e.g. "A,B,C" - an identity for each slide.</summary>
    private static string Paths(MediaGroupItem g) =>
        string.Join(",", g.Items.Select(i => i is MediaGroupItem.MediaItem m ? m.SourceMediaFilePath : "<slide>"));

    // ---------------- delete ----------------

    [TestMethod]
    public void DeleteSlide_UndoRestoresAtSamePosition_RedoDeletesAgain()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");

        GroupSlideEdits.RemoveAt(g, 1);
        Assert.AreEqual("A,C", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A,B,C", Paths(g));

        p.UndoStack.Redo();
        Assert.AreEqual("A,C", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A,B,C", Paths(g));
    }

    [TestMethod]
    public void DeleteSlide_UndoMenuTextNamesTheAction()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B");

        GroupSlideEdits.RemoveAt(g, 0, "Delete Slide");

        StringAssert.Contains(p.UndoStack.UndoMenuText, "Delete Slide");
    }

    // ---------------- insert / duplicate ----------------

    [TestMethod]
    public void InsertSlide_UndoRemovesIt_RedoRestoresIt()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "C");

        GroupSlideEdits.Insert(g, 1, Media("B"));
        Assert.AreEqual("A,B,C", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A,C", Paths(g));

        p.UndoStack.Redo();
        Assert.AreEqual("A,B,C", Paths(g));
    }

    [TestMethod]
    public void InsertSlide_PastTheEnd_IsClampedAndUndoable()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A");

        GroupSlideEdits.Insert(g, 99, Media("B"));
        Assert.AreEqual("A,B", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A", Paths(g));
    }

    [TestMethod]
    public void DuplicateSlide_UndoRemovesOnlyTheCopy()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B");

        var copy = g.Items[0].Clone();
        GroupSlideEdits.Insert(g, 1, copy, "Duplicate Slide");
        Assert.AreEqual("A,A,B", Paths(g));
        StringAssert.Contains(p.UndoStack.UndoMenuText, "Duplicate Slide");

        p.UndoStack.Undo();
        Assert.AreEqual("A,B", Paths(g));
    }

    // ---------------- move ----------------

    [TestMethod]
    public void MoveSlide_UndoMovesBack()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");

        GroupSlideEdits.Move(g, 0, 2);
        Assert.AreEqual("B,C,A", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A,B,C", Paths(g));

        p.UndoStack.Redo();
        Assert.AreEqual("B,C,A", Paths(g));
    }

    [TestMethod]
    public void MoveSlide_ToSamePosition_RecordsNothing()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B");

        GroupSlideEdits.Move(g, 1, 1);

        Assert.IsFalse(p.UndoStack.CanUndo);
    }

    [TestMethod]
    public void MoveSlideAcrossGroups_IsOneUndoStep()
    {
        var p = new PlaylistInstance();
        var src = Group(p, "S", "A", "B", "C");
        var dst = Group(p, "D", "X", "Y");

        var item = src.Items[1];
        using (GroupSlideEdits.BeginTransaction(dst, "Move Slide"))
        {
            GroupSlideEdits.Insert(dst, 1, item, "Move Slide");
            GroupSlideEdits.RemoveAt(src, 1, "Move Slide");
        }

        Assert.AreEqual("A,C", Paths(src));
        Assert.AreEqual("X,B,Y", Paths(dst));

        p.UndoStack.Undo();
        Assert.AreEqual("A,B,C", Paths(src));
        Assert.AreEqual("X,Y", Paths(dst));
        Assert.IsFalse(p.UndoStack.CanUndo, "the whole move was a single step");

        p.UndoStack.Redo();
        Assert.AreEqual("A,C", Paths(src));
        Assert.AreEqual("X,B,Y", Paths(dst));
    }

    [TestMethod]
    public void AddSeveralFiles_InOneTransaction_IsOneUndoStep()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A");

        using (GroupSlideEdits.BeginTransaction(g, "Add Media"))
        {
            GroupSlideEdits.Insert(g, 1, Media("B"), regenerate: false);
            GroupSlideEdits.Insert(g, 2, Media("C"), regenerate: false);
        }

        Assert.AreEqual("A,B,C", Paths(g));
        p.UndoStack.Undo();
        Assert.AreEqual("A", Paths(g));
    }

    // ---------------- change media ----------------

    [TestMethod]
    public void ChangeMedia_UndoRestoresThePreviousFile()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B");

        GroupSlideEdits.SetMediaPath(g, 1, "B2");
        Assert.AreEqual("A,B2", Paths(g));

        p.UndoStack.Undo();
        Assert.AreEqual("A,B", Paths(g));

        p.UndoStack.Redo();
        Assert.AreEqual("A,B2", Paths(g));
    }

    // ---------------- selection ----------------

    [TestMethod]
    public void DeleteSlide_KeepsTheSelectedSlideSelected_AndUndoDoesToo()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "G" };
        for (var i = 0; i < 3; i++) g.Items.Add(new MediaGroupItem.SlideItem { SlideData = new CustomSlide() });
        p.Items.Add(g);
        g.GenerateSlides();
        g.SelectedSlideIndex = 2;
        var selected = g.Slides[2];

        GroupSlideEdits.RemoveAt(g, 0);
        Assert.AreSame(selected, g.Slides[g.SelectedSlideIndex], "selection follows the slide, not the index");

        p.UndoStack.Undo();
        Assert.AreEqual(3, g.Slides.Count);
        Assert.AreSame(selected, g.Slides[g.SelectedSlideIndex]);
    }

    [TestMethod]
    public void DeleteSelectedSlide_UndoReselectsIt()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "G" };
        for (var i = 0; i < 3; i++) g.Items.Add(new MediaGroupItem.SlideItem { SlideData = new CustomSlide() });
        p.Items.Add(g);
        g.GenerateSlides();
        g.SelectedSlideIndex = 1;
        var selected = g.Slides[1];

        GroupSlideEdits.RemoveAt(g, 1);
        p.UndoStack.Undo();

        Assert.AreEqual(1, g.SelectedSlideIndex);
        Assert.AreSame(selected, g.Slides[1], "the very same slide object comes back");
    }

    // ---------------- interplay with other history ----------------

    [TestMethod]
    public void SlideEdit_StillUndoable_AfterTheWholeGroupWasDeletedAndRestored()
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");

        GroupSlideEdits.RemoveAt(g, 1); // A,C
        p.UndoStack.Execute(new RemovePlaylistItemAction(p, g));
        p.UndoStack.Undo(); // the group is back, rebuilt as a fresh instance

        var rebuilt = (MediaGroupItemInstance)p.Items.Single();
        Assert.AreNotSame(g, rebuilt, "the group really was rebuilt");
        Assert.AreEqual("A,C", Paths(rebuilt));

        p.UndoStack.Undo(); // undo the slide delete, now on the rebuilt group
        Assert.AreEqual("A,B,C", Paths(rebuilt));

        p.UndoStack.Redo();
        Assert.AreEqual("A,C", Paths(rebuilt));
    }

    [TestMethod]
    public void SlideEdit_ThenUndoToCleanPlaylist_ClearsDirty()
    {
        var p = new PlaylistInstance { PlaylistFilePath = Path.Combine(_tempDir, "p.xml") };
        var g = Group(p, "G", "A", "B", "C");
        PlaylistDocumentService.SaveDocument(p);

        GroupSlideEdits.RemoveAt(g, 1);
        Assert.IsTrue(p.DirtyTracker.Refresh());

        p.UndoStack.Undo();
        Assert.IsFalse(p.DirtyTracker.Refresh(), "back to exactly the saved state");

        GroupSlideEdits.Move(g, 0, 2);
        Assert.IsTrue(p.DirtyTracker.Refresh());
        p.UndoStack.Undo();
        Assert.IsFalse(p.DirtyTracker.Refresh());
    }

    // ---------------- outside a playlist ----------------

    [TestMethod]
    public void GroupNotInAPlaylist_EditsApplyDirectly_WithoutHistory()
    {
        var standalone = new MediaGroupItemInstance(new PlaylistInstance()) { Title = "Loose" };
        standalone.Items.Add(Media("A"));
        standalone.Items.Add(Media("B"));

        GroupSlideEdits.RemoveAt(standalone, 0);

        Assert.AreEqual("B", Paths(standalone));
        Assert.IsNull(GroupSlideEdits.PlaylistOf(standalone));
    }

    [TestMethod]
    public void RegenerateFalse_LeavesGeneratedSlidesAlone_UntilUndo()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "G" };
        g.Items.Add(new MediaGroupItem.SlideItem { SlideData = new CustomSlide() });
        g.Items.Add(new MediaGroupItem.SlideItem { SlideData = new CustomSlide() });
        p.Items.Add(g);
        g.GenerateSlides();

        GroupSlideEdits.RemoveAt(g, 0, regenerate: false);
        Assert.AreEqual(1, g.Items.Count);
        Assert.AreEqual(2, g.Slides.Count, "editor windows rebuild on Apply");

        p.UndoStack.Undo(); // undo always rebuilds
        Assert.AreEqual(2, g.Items.Count);
        Assert.AreEqual(2, g.Slides.Count);
    }
}
