using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

/// <summary>
/// Imports (Sync / re-import) and "Load" replace a group's whole slide list outside the undo system. Steps that
/// address slides by position must be dropped then, or they could hit the wrong slide.
/// </summary>
[TestClass]
public class ImportInteractionTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImportInteractionTests_" + Guid.NewGuid().ToString("N"));
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
        p.UndoStack.Clear();
        return g;
    }

    private static TrulyObservableCollection<MediaGroupItem.GroupItem> NewSlides(params string[] paths) =>
        new(paths.Select(p => (MediaGroupItem.GroupItem)Media(p)).ToList());

    // ---------------- stale position-based steps are dropped ----------------

    [TestMethod]
    public Task ReplacingTheSlideList_DropsThatGroupsSlideSteps_ButKeepsEverythingElse() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");
        var other = Group(p, "Other", "X", "Y");

        p.Title = "Renamed";                 // by identity: stays
        GroupSlideEdits.RemoveAt(g, 1);      // "delete slide 2 of G": by position
        GroupSlideEdits.RemoveAt(other, 0);  // a different group: stays
        Assert.AreEqual(3, UndoCount(p));

        g.Items = NewSlides("P", "Q"); // a re-import finishes

        Assert.AreEqual(2, UndoCount(p), "only G's positional step is dropped");
        p.UndoStack.Undo(); // the other group's step
        Assert.AreEqual("X,Y", Paths(other));
        p.UndoStack.Undo(); // the rename
        Assert.AreNotEqual("Renamed", p.Title);
        Assert.IsFalse(p.UndoStack.CanUndo);
        Assert.AreEqual("P,Q", Paths(g), "the replaced list was left alone");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task InPlaceEdits_AreNotMistakenForAReplacement() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");

        GroupSlideEdits.RemoveAt(g, 0);
        GroupSlideEdits.Insert(g, 0, Media("Z"), "Add Slide", regenerate: false);
        GroupSlideEdits.Move(g, 0, 1);

        Assert.AreEqual(3, UndoCount(p), "slide edits raise Items-changed too; none of them may drop history");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task UndoAndRedo_DoNotDropTheirOwnSteps() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");
        GroupSlideEdits.RemoveAt(g, 1);
        GroupSlideEdits.RemoveAt(g, 0);

        p.UndoStack.Undo();
        p.UndoStack.Undo();
        Assert.AreEqual("A,B,C", Paths(g));
        p.UndoStack.Redo();
        Assert.IsTrue(p.UndoStack.CanUndo);
        Assert.IsTrue(p.UndoStack.CanRedo);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task RedoSideStepsOfTheReplacedGroup_AreDroppedToo() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");
        GroupSlideEdits.RemoveAt(g, 1);
        p.UndoStack.Undo(); // now a redo step
        Assert.IsTrue(p.UndoStack.CanRedo);

        g.Items = NewSlides("P");
        Assert.IsFalse(p.UndoStack.CanRedo);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AStepSpanningTwoGroups_IsDroppedWhenEitherIsReplaced() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var a = Group(p, "A", "1", "2");
        var b = Group(p, "B", "3", "4");

        using (p.UndoStack.BeginTransaction("Move Slide"))
        {
            GroupSlideEdits.RemoveAt(a, 0);
            GroupSlideEdits.Insert(b, 0, Media("1"), "Add Slide", regenerate: false);
        }

        Assert.AreEqual(1, UndoCount(p));
        b.Items = NewSlides("9");
        Assert.AreEqual(0, UndoCount(p));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AReplacementThatFinishesOnABackgroundThread_StillDropsTheSteps() => DispatcherTestThread.Run(async () =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");
        GroupSlideEdits.RemoveAt(g, 1);

        // a Sync finishes on the import worker thread
        await Task.Run(() => g.Items = NewSlides("P"));
        for (var i = 0; i < 40 && p.UndoStack.CanUndo; i++) await Task.Delay(25); // the hand-back is posted to this thread

        Assert.IsFalse(p.UndoStack.CanUndo);
    });

    [TestMethod]
    public Task DeletingAndRestoringTheGroup_KeepsItsEarlierSlideSteps() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        var g = Group(p, "G", "A", "B", "C");
        GroupSlideEdits.RemoveAt(g, 1);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, g));
        p.UndoStack.Undo(); // the group comes back as a new instance: not an import replacing its slides

        Assert.AreEqual(1, UndoCount(p), "the slide step is still there (the group delete itself is now a redo step)");
        p.UndoStack.Undo(); // undo the delete of slide B on the restored group
        Assert.AreEqual("A,B,C", Paths((MediaGroupItem)p.Items.Single()));
        return Task.CompletedTask;
    });

    // ---------------- restored items that still need their import ----------------

    [TestMethod]
    public void RestoredImportedItemWithNoSlides_NeedsItsImportStarted()
    {
        var p = new PlaylistInstance();
        Assert.IsTrue(PlaylistItemSnapshot.NeedsImportAfterRestore(new PDFSlidesGroupItemInstance(p)));
        Assert.IsTrue(PlaylistItemSnapshot.NeedsImportAfterRestore(new PowerPointPresentationItemInstance(p)));
        Assert.IsTrue(PlaylistItemSnapshot.NeedsImportAfterRestore(new GoogleSlidesGroupItemInstance(p)));
    }

    [TestMethod]
    public void RestoredImportedItemWhoseFilesAreThere_NeedsNothing()
    {
        var p = new PlaylistInstance();
        var slide = Path.Combine(_tempDir, "slide1.png");
        File.WriteAllText(slide, "png");
        var pdf = new PDFSlidesGroupItemInstance(p);
        pdf.Items.Add(Media(slide));

        Assert.IsFalse(PlaylistItemSnapshot.NeedsImportAfterRestore(pdf));
    }

    [TestMethod]
    public void RestoredImportedItemWhoseFilesWentMissing_NeedsItsImportStarted()
    {
        var p = new PlaylistInstance();
        var pdf = new PDFSlidesGroupItemInstance(p);
        pdf.Items.Add(Media(Path.Combine(_tempDir, "gone.png")));

        Assert.IsTrue(PlaylistItemSnapshot.NeedsImportAfterRestore(pdf));
    }

    [TestMethod]
    public void RestoredPlainGroupOrOtherItem_NeverStartsAnImport()
    {
        var p = new PlaylistInstance();
        Assert.IsFalse(PlaylistItemSnapshot.NeedsImportAfterRestore(new MediaGroupItemInstance(p))); // empty, but not imported
        Assert.IsFalse(PlaylistItemSnapshot.NeedsImportAfterRestore(new LogoItemInstance(p)));
        Assert.IsFalse(PlaylistItemSnapshot.NeedsImportAfterRestore(new SectionHeadingItem { Title = "Hi" }));
    }

    [TestMethod]
    public Task UndoingTheDeleteOfAnImportedItemWithNoSlides_StartsItsImport() => DispatcherTestThread.Run(() =>
    {
        var started = new List<Guid>();
        var original = PlaylistItemSnapshot.StartImport;
        PlaylistItemSnapshot.StartImport = s => started.Add(((Item)s).UUID);
        try
        {
            var p = new PlaylistInstance();
            var pdf = new PDFSlidesGroupItemInstance(p); // still importing: no slides yet
            var plain = new MediaGroupItemInstance(p) { Title = "Plain, empty" };
            p.Items.Add(pdf);
            p.Items.Add(plain);
            p.UndoStack.Clear();

            p.UndoStack.Execute(new RemovePlaylistItemAction(p, pdf));
            p.UndoStack.Undo();
            Assert.AreEqual(1, started.Count, "the restored PDF item is brought up to date, like on playlist load");
            Assert.AreEqual(pdf.UUID, started[0]);

            p.UndoStack.Execute(new RemovePlaylistItemAction(p, plain));
            p.UndoStack.Undo();
            Assert.AreEqual(1, started.Count, "a plain group is not imported from anywhere");
        }
        finally
        {
            PlaylistItemSnapshot.StartImport = original;
        }

        return Task.CompletedTask;
    });

    // ---------------- helpers ----------------

    private static string Paths(MediaGroupItem g) =>
        string.Join(",", g.Items.Select(i => i is MediaGroupItem.MediaItem m ? m.SourceMediaFilePath : "<slide>"));

    /// <summary>How many steps Undo can currently walk back through.</summary>
    private static int UndoCount(PlaylistInstance p)
    {
        // peek by undoing everything, then redoing it all
        var n = 0;
        while (p.UndoStack.Undo()) n++;
        for (var i = 0; i < n; i++) p.UndoStack.Redo();
        return n;
    }
}
