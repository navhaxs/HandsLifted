using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class PlaylistItemActionsTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistItemActionsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static LogoItemInstance Logo(PlaylistInstance p, string title) => new(p) { Title = title };
    private static string Order(PlaylistInstance p) => string.Concat(p.Items.Select(i => i.Title));

    private PlaylistInstance ThreeLogos(out LogoItemInstance a, out LogoItemInstance b, out LogoItemInstance c)
    {
        var p = new PlaylistInstance();
        a = Logo(p, "A");
        b = Logo(p, "B");
        c = Logo(p, "C");
        p.Items.Add(a);
        p.Items.Add(b);
        p.Items.Add(c);
        return p;
    }

    // ---------------- insert ----------------

    [TestMethod]
    public void Insert_Undo_Redo()
    {
        var p = ThreeLogos(out _, out _, out _);
        var d = Logo(p, "D");

        p.UndoStack.Execute(new InsertPlaylistItemAction(p, d, 1));
        Assert.AreEqual("ADBC", Order(p));
        Assert.AreSame(d, p.Items[1], "first execution inserts the very instance the caller built");

        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));

        p.UndoStack.Redo();
        Assert.AreEqual("ADBC", Order(p));
        Assert.AreEqual(d.UUID, p.Items[1].UUID, "redo brings back the same item identity");
    }

    [TestMethod]
    public void Insert_PlainDataItem_Works()
    {
        var p = ThreeLogos(out _, out _, out _);
        var heading = new SectionHeadingItem { Title = "Worship" };

        p.UndoStack.Execute(new InsertPlaylistItemAction(p, heading, 0));
        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));

        p.UndoStack.Redo();
        Assert.AreEqual("WorshipABC", Order(p));
        Assert.AreEqual(heading.UUID, p.Items[0].UUID);
    }

    [TestMethod]
    public void Insert_KeepsExistingSelection()
    {
        var p = ThreeLogos(out _, out var b, out _);
        p.SelectedItemIndex = 1; // B

        p.UndoStack.Execute(new InsertPlaylistItemAction(p, Logo(p, "D"), 0));
        Assert.AreSame(b, p.Items[p.SelectedItemIndex]);

        p.UndoStack.Undo();
        Assert.AreSame(b, p.Items[p.SelectedItemIndex]);
    }

    [TestMethod]
    public void Insert_IndexBeyondEnd_Appends()
    {
        var p = ThreeLogos(out _, out _, out _);

        p.UndoStack.Execute(new InsertPlaylistItemAction(p, Logo(p, "D"), 99));

        Assert.AreEqual("ABCD", Order(p));
    }

    [TestMethod]
    public void Insert_MultipleInOneTransaction_IsOneStep_AndKeepsOrder()
    {
        var p = ThreeLogos(out _, out _, out _);

        // what dropping two files at index 1 does: each inserted at the same index
        using (p.UndoStack.BeginTransaction("Add Items"))
        {
            p.UndoStack.Execute(new InsertPlaylistItemAction(p, Logo(p, "Y"), 1));
            p.UndoStack.Execute(new InsertPlaylistItemAction(p, Logo(p, "X"), 1));
        }

        Assert.AreEqual("AXYBC", Order(p));
        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));
        Assert.IsFalse(p.UndoStack.CanUndo, "one undo reverted both");

        p.UndoStack.Redo();
        Assert.AreEqual("AXYBC", Order(p));
    }

    // ---------------- remove ----------------

    [TestMethod]
    public void Remove_Undo_RestoresAtSameIndex()
    {
        var p = ThreeLogos(out _, out var b, out _);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        Assert.AreEqual("AC", Order(p));

        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));
        Assert.AreEqual(b.UUID, p.Items[1].UUID);
    }

    [TestMethod]
    public void Remove_Undo_RestoresEditsMadeBeforeDeleting()
    {
        var p = ThreeLogos(out _, out var b, out _);
        b.Title = "Renamed";
        b.SlideTransitionDurationMs = 450;

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        p.UndoStack.Undo();

        Assert.AreEqual("Renamed", p.Items[1].Title);
        Assert.AreEqual(450, p.Items[1].SlideTransitionDurationMs);
    }

    [TestMethod]
    public void Remove_UndoRedoUndo_StaysConsistent()
    {
        var p = ThreeLogos(out _, out var b, out _);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        p.UndoStack.Undo();
        p.UndoStack.Redo();
        Assert.AreEqual("AC", Order(p));

        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));
        Assert.AreEqual(b.UUID, p.Items[1].UUID);
    }

    [TestMethod]
    public void Remove_OfSelectedItem_UndoReselectsIt()
    {
        var p = ThreeLogos(out _, out var b, out _);
        p.SelectedItemIndex = 1;

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        p.UndoStack.Undo();

        Assert.AreEqual(1, p.SelectedItemIndex);
        Assert.AreEqual(b.UUID, p.Items[p.SelectedItemIndex].UUID);
    }

    [TestMethod]
    public void Remove_OfUnselectedItem_KeepsSelectionOnUndo()
    {
        var p = ThreeLogos(out var a, out var b, out _);
        p.SelectedItemIndex = 0; // A

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        Assert.AreSame(a, p.Items[p.SelectedItemIndex]);

        p.UndoStack.Undo();
        Assert.AreSame(a, p.Items[p.SelectedItemIndex]);
    }

    [TestMethod]
    public void Remove_ThenMoveThenUndoBoth_RestoresOriginalOrder()
    {
        var p = ThreeLogos(out var a, out var b, out _);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));           // AC
        p.UndoStack.Execute(new MovePlaylistItemAction(p, a, 0, 1));       // CA

        p.UndoStack.Undo();
        p.UndoStack.Undo();

        Assert.AreEqual("ABC", Order(p));
    }

    // ---------------- dirty tracking interplay ----------------

    [TestMethod]
    public void AddThenUndo_ReturnsPlaylistToClean()
    {
        var p = ThreeLogos(out _, out _, out _);
        p.PlaylistFilePath = Path.Combine(_tempDir, "p.xml");
        PlaylistDocumentService.SaveDocument(p);

        p.UndoStack.Execute(new InsertPlaylistItemAction(p, Logo(p, "D"), 3));
        Assert.IsTrue(p.DirtyTracker.Refresh());

        p.UndoStack.Undo();
        Assert.IsFalse(p.DirtyTracker.Refresh(), "back to exactly the saved state");
    }

    // ---------------- fidelity: delete + undo must reproduce the saved state exactly ----------------

    private static SongItem RegisterSong(string title)
    {
        var song = new SongItem { Title = title };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        return song;
    }

    private static IEnumerable<(string Name, Func<PlaylistInstance, Item> Make)> OneOfEachItemType()
    {
        yield return ("Logo", p => new LogoItemInstance(p) { Title = "Logo", SlideTransitionDurationMs = 300 });
        yield return ("SectionHeading", p => new SectionHeadingItem { Title = "Heading" });
        yield return ("Comment", p => new CommentItem { Title = "Remember the offering" });
        yield return ("Song", p => new SongItemInstance(p)
        {
            SongId = RegisterSong("Amazing Grace").UUID,
            SlideTransitionDurationMs = 250,
            MotionBackgroundVideoOverride = @"C:\Videos\bg.mp4"
        });
        yield return ("Scripture", p => new ScriptureItemInstance(p)
        {
            Title = "John 3:16", Book = "JHN", Translation = "BSB",
            StartChapter = 3, StartVerse = 16, EndChapter = 3, EndVerse = 16
        });
        yield return ("MediaGroup", p =>
        {
            var g = new MediaGroupItemInstance(p) { Title = "Photos" };
            g.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png", Meta = new() { Text = "caption A" } });
            g.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\b.png" });
            g.AutoAdvanceTimer.IsEnabled = true;
            g.AutoAdvanceTimer.IntervalMs = 7000;
            return g;
        });
        yield return ("PDF", p => new PDFSlidesGroupItemInstance(p) { Title = "Slides", SourcePresentationFile = @"C:\deck.pdf" });
        yield return ("PowerPoint", p => new PowerPointPresentationItemInstance(p) { Title = "Deck", SourcePresentationFile = @"C:\deck.pptx" });
        yield return ("GoogleSlides", p => new GoogleSlidesGroupItemInstance(p) { Title = "G", SourceGooglePresentationId = "abc123" });
        yield return ("OnlineVideo", p => new OnlineVideoItemInstance(p) { Title = "Clip", SourceVideoUrl = "https://example.com/v" });
    }

    [TestMethod]
    public void DeleteThenUndo_ReproducesSavedStateExactly_ForEveryItemType()
    {
        var problems = new List<string>();

        foreach (var (name, make) in OneOfEachItemType())
        {
            try
            {
                var p = new PlaylistInstance { Title = "p", PlaylistFilePath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".xml") };
                p.Items.Add(Logo(p, "before"));
                var item = make(p);
                p.Items.Add(item);
                p.Items.Add(Logo(p, "after"));
                PlaylistDocumentService.SaveDocument(p);
                var id = item.UUID;

                p.UndoStack.Execute(new RemovePlaylistItemAction(p, item));
                if (p.Items.Any(i => i.UUID == id)) problems.Add($"{name}: still present after delete");
                if (!p.DirtyTracker.Refresh()) problems.Add($"{name}: delete did not register as a change");

                p.UndoStack.Undo();
                if (p.Items.Count != 3 || p.Items[1].UUID != id) problems.Add($"{name}: not restored at index 1 with the same UUID");
                if (p.DirtyTracker.Refresh()) problems.Add($"{name}: restored item serializes differently from the saved state");

                // and the whole cycle again (restore from a second snapshot)
                p.UndoStack.Redo();
                p.UndoStack.Undo();
                if (p.DirtyTracker.Refresh()) problems.Add($"{name}: differs after a second delete/undo cycle");
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null) root = root.InnerException;
                problems.Add($"{name}: THREW {root.GetType().Name}: {root.Message}");
            }
        }

        Assert.AreEqual(0, problems.Count, "Fidelity problems:\n" + string.Join("\n", problems));
    }

    [TestMethod]
    public void SectionHeadingAndComment_KeepTheirColour_ThroughXmlCloneAndRestore()
    {
        // Regression: the derived ItemGroupAvaloniaColor property used to be XML-serialized; reading it back
        // assigned default(Color), which converts to null and wiped GroupColour on every Clone()/load.
        var p = new PlaylistInstance();
        var heading = new SectionHeadingItem { Title = "H" };
        var comment = new CommentItem { Title = "C" };
        p.Items.Add(heading);
        p.Items.Add(comment);

        var headingClone = (SectionHeadingItem)heading.Clone();
        var commentClone = (CommentItem)comment.Clone();

        Assert.IsNotNull(headingClone.GroupColour);
        Assert.IsNotNull(commentClone.GroupColour);
        Assert.AreEqual(heading.ItemGroupAvaloniaColor, headingClone.ItemGroupAvaloniaColor);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, heading));
        p.UndoStack.Undo();
        Assert.AreEqual(heading.ItemGroupAvaloniaColor, ((SectionHeadingItem)p.Items[0]).ItemGroupAvaloniaColor);
    }

    [TestMethod]
    public void DeleteThenUndo_Song_StillResolvesToLibrarySong()
    {
        var song = RegisterSong("How Great Thou Art");
        var p = new PlaylistInstance();
        var item = new SongItemInstance(p) { SongId = song.UUID };
        p.Items.Add(item);

        p.UndoStack.Execute(new RemovePlaylistItemAction(p, item));
        p.UndoStack.Undo();

        var restored = (SongItemInstance)p.Items[0];
        Assert.AreEqual(song.UUID, restored.SongId);
        Assert.IsFalse(restored.IsMissing);
        Assert.AreNotSame(item, restored, "a fresh instance - the removed one was disposed");
    }
}
