using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
public class PropertyEditTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PropertyEditTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static LogoItemInstance Logo(PlaylistInstance p, string title)
    {
        var item = new LogoItemInstance(p) { Title = title };
        p.Items.Add(item);
        return item;
    }

    /// <summary>A playlist with the given starting values, and an empty history (initializer assignments aren't edits).</summary>
    private static PlaylistInstance Fresh(string? title = null, double? fadeMs = null, string? filePath = null)
    {
        var p = new PlaylistInstance();
        if (title != null) p.Title = title;
        if (fadeMs != null) p.SlideTransitionDurationMs = fadeMs.Value;
        if (filePath != null) p.PlaylistFilePath = filePath;
        p.UndoStack.Clear();
        return p;
    }

    // ---------------- playlist-level ----------------

    [TestMethod]
    public void PlaylistTitle_UndoRedo()
    {
        var p = Fresh(title: "Sunday");

        p.Title = "Sunday Evening";
        Assert.IsTrue(p.UndoStack.CanUndo);
        Assert.AreEqual("Undo Rename Playlist", p.UndoStack.UndoMenuText);

        p.UndoStack.Undo();
        Assert.AreEqual("Sunday", p.Title);

        p.UndoStack.Redo();
        Assert.AreEqual("Sunday Evening", p.Title);
    }

    [TestMethod]
    public void TypingATitle_IsOneUndoStep()
    {
        var p = Fresh(title: "");

        foreach (var typed in new[] { "S", "Su", "Sun", "Sund", "Sunda", "Sunday" }) p.Title = typed;

        p.UndoStack.Undo();
        Assert.AreEqual("", p.Title);
        Assert.IsFalse(p.UndoStack.CanUndo, "all keystrokes merged into one step");
    }

    [TestMethod]
    public void DraggingTheFadeSlider_IsOneUndoStep_BackToTheOriginalValue()
    {
        var p = Fresh(fadeMs: 200);

        for (var ms = 250; ms <= 800; ms += 50) p.SlideTransitionDurationMs = ms;

        p.UndoStack.Undo();
        Assert.AreEqual(200, p.SlideTransitionDurationMs);
        Assert.IsFalse(p.UndoStack.CanUndo);

        p.UndoStack.Redo();
        Assert.AreEqual(800, p.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void ThemeAssignment_UndoRestoresThePreviousOverride()
    {
        var p = new PlaylistInstance();
        var themeA = Guid.NewGuid();
        var themeB = Guid.NewGuid();
        p.DefaultSongThemeId = themeA;
        p.UndoStack.Clear();

        p.DefaultSongThemeId = themeB;
        p.UndoStack.Undo();
        Assert.AreEqual(themeA, p.DefaultSongThemeId);

        p.UndoStack.Redo();
        p.DefaultSongThemeId = null; // "clear the override"
        p.UndoStack.Undo();
        Assert.AreEqual(themeB, p.DefaultSongThemeId);
    }

    [TestMethod]
    public void DifferentPlaylistProperties_AreSeparateSteps()
    {
        var p = Fresh(title: "A", fadeMs: 200);

        p.Title = "B";
        p.SlideTransitionDurationMs = 400;

        p.UndoStack.Undo();
        Assert.AreEqual(200, p.SlideTransitionDurationMs);
        Assert.AreEqual("B", p.Title);

        p.UndoStack.Undo();
        Assert.AreEqual("A", p.Title);
    }

    // ---------------- items ----------------

    [TestMethod]
    public void ItemTitle_UndoRedo_ByUuid()
    {
        var p = new PlaylistInstance();
        var a = Logo(p, "A");
        var b = Logo(p, "B");
        p.UndoStack.Clear();

        b.Title = "B renamed";
        a.Title = "A renamed";
        StringAssert.Contains(p.UndoStack.UndoMenuText, "Rename");

        p.UndoStack.Undo(); // A
        Assert.AreEqual("A", a.Title);
        Assert.AreEqual("B renamed", b.Title);

        p.UndoStack.Undo(); // B
        Assert.AreEqual("B", b.Title);
    }

    [TestMethod]
    public void EditingTwoDifferentItems_DoesNotMerge()
    {
        var p = new PlaylistInstance();
        var a = Logo(p, "A");
        var b = Logo(p, "B");
        p.UndoStack.Clear();

        a.Title = "A2";
        b.Title = "B2";

        p.UndoStack.Undo();
        Assert.AreEqual("A2", a.Title, "only B's edit was undone");
        Assert.AreEqual("B", b.Title);
    }

    [TestMethod]
    public void ItemFadeOverride_NullToValueToNull()
    {
        var p = new PlaylistInstance();
        var a = Logo(p, "A");
        p.UndoStack.Clear();
        Assert.IsNull(a.SlideTransitionDurationMs);

        a.SlideTransitionDurationMs = 450;
        p.UndoStack.Undo();
        Assert.IsNull(a.SlideTransitionDurationMs, "override removed again");

        p.UndoStack.Redo();
        Assert.AreEqual(450, a.SlideTransitionDurationMs);
    }

    [TestMethod]
    public void AutoAdvanceTimer_EnabledAndInterval_AreUndoable()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "Photos" };
        p.Items.Add(g);
        p.UndoStack.Clear();

        g.AutoAdvanceTimer.IsEnabled = true;
        g.AutoAdvanceTimer.IntervalMs = 5000;

        p.UndoStack.Undo();
        Assert.AreEqual(3000, g.AutoAdvanceTimer.IntervalMs);
        Assert.IsTrue(g.AutoAdvanceTimer.IsEnabled);

        p.UndoStack.Undo();
        Assert.IsFalse(g.AutoAdvanceTimer.IsEnabled);

        p.UndoStack.Redo();
        p.UndoStack.Redo();
        Assert.IsTrue(g.AutoAdvanceTimer.IsEnabled);
        Assert.AreEqual(5000, g.AutoAdvanceTimer.IntervalMs);
    }

    [TestMethod]
    public void AutoAdvanceTimer_ReplacedObject_IsFollowed()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p);
        p.Items.Add(g);
        p.UndoStack.Clear();

        g.AutoAdvanceTimer = new ItemAutoAdvanceTimerForTest { IsEnabled = false };
        p.UndoStack.Clear(); // swapping the object itself isn't an undoable edit

        g.AutoAdvanceTimer.IntervalMs = 9000;
        p.UndoStack.Undo();
        Assert.AreEqual(3000, g.AutoAdvanceTimer.IntervalMs);
    }

    private sealed class ItemAutoAdvanceTimerForTest : HandsLiftedApp.Data.Data.Models.Items.ItemAutoAdvanceTimer { }

    [TestMethod]
    public void SongBackgroundVideoOverride_IsUndoable_ButSongTitleIsNotRecorded()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var p = new PlaylistInstance();
        var item = new SongItemInstance(p) { SongId = song.UUID };
        p.Items.Add(item);
        p.UndoStack.Clear();

        item.Title = "Renamed in the library"; // shared library song: not a playlist edit
        Assert.IsFalse(p.UndoStack.CanUndo);

        item.MotionBackgroundVideoOverride = @"C:\Videos\bg.mp4";
        Assert.IsTrue(p.UndoStack.CanUndo);
        p.UndoStack.Undo();
        Assert.IsNull(item.MotionBackgroundVideoOverride);
    }

    // ---------------- what must NOT be recorded ----------------

    [TestMethod]
    public void UndoItself_IsNotRecorded_AndTheNextEditStillHasTheRightOldValue()
    {
        var p = Fresh(title: "A");

        p.Title = "B";
        p.UndoStack.Undo(); // back to A
        Assert.IsFalse(p.UndoStack.CanUndo);
        Assert.IsTrue(p.UndoStack.CanRedo);

        p.Title = "C";
        Assert.IsFalse(p.UndoStack.CanRedo, "a new edit clears redo");
        p.UndoStack.Undo();
        Assert.AreEqual("A", p.Title, "old value was A, not the stale B");
    }

    [TestMethod]
    public void SuppressedChanges_AreNotRecorded_ButNextEditHasTheRightOldValue()
    {
        var p = new PlaylistInstance();
        var a = Logo(p, "A");
        p.UndoStack.Clear();

        using (p.PropertyRecorder.Suppress())
        {
            a.Title = "From an import";
        }

        Assert.IsFalse(p.UndoStack.CanUndo);

        a.Title = "User edit";
        p.UndoStack.Undo();
        Assert.AreEqual("From an import", a.Title);
    }

    [TestMethod]
    public void WhileLoading_NothingIsRecorded()
    {
        var p = Fresh(title: "Old");

        p.IsPlaylistLoading = true;
        p.Title = "Loaded title";
        p.SlideTransitionDurationMs = 123;
        p.IsPlaylistLoading = false;

        Assert.IsFalse(p.UndoStack.CanUndo);
    }

    [TestMethod]
    public async Task ChangesFromAnotherThread_AreNotRecorded()
    {
        var p = Fresh(title: "A");

        await Task.Run(() => p.Title = "Set by a background job");

        Assert.IsFalse(p.UndoStack.CanUndo);
    }

    [TestMethod]
    public void ItemAddedBeforeTheEdit_IsWatched_ItemRemovedIsNot()
    {
        var p = new PlaylistInstance();
        var a = Logo(p, "A");
        p.UndoStack.Clear();

        p.Items.Remove(a);
        p.UndoStack.Clear();
        a.Title = "edited while detached";

        Assert.IsFalse(p.UndoStack.CanUndo);
    }

    // ---------------- interplay with other history ----------------

    [TestMethod]
    public void TitleEdit_StillUndoable_AfterTheItemWasDeletedAndRestored()
    {
        var p = new PlaylistInstance();
        Logo(p, "A");
        var b = Logo(p, "B");
        Logo(p, "C");
        p.UndoStack.Clear();

        b.Title = "B renamed";
        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        p.UndoStack.Undo(); // B is back as a fresh instance

        var rebuilt = p.Items[1];
        Assert.AreNotSame(b, rebuilt);
        Assert.AreEqual("B renamed", rebuilt.Title);

        p.UndoStack.Undo(); // the rename, on the rebuilt instance
        Assert.AreEqual("B", rebuilt.Title);

        p.UndoStack.Redo();
        Assert.AreEqual("B renamed", rebuilt.Title);
    }

    [TestMethod]
    public void EditsToARestoredItem_AreRecorded()
    {
        var p = new PlaylistInstance();
        var b = Logo(p, "B");
        p.UndoStack.Execute(new RemovePlaylistItemAction(p, b));
        p.UndoStack.Undo();
        p.UndoStack.Clear();

        p.Items[0].Title = "after restore";
        p.UndoStack.Undo();

        Assert.AreEqual("B", p.Items[0].Title);
    }

    [TestMethod]
    public void ReplacingTheItemCollection_ReattachesTheRecorder()
    {
        var p = new PlaylistInstance();
        p.Items = new PlaylistItemInstanceCollection<Item>(); // what loading a file does
        var a = Logo(p, "A");
        p.UndoStack.Clear();

        a.Title = "A2";
        p.UndoStack.Undo();

        Assert.AreEqual("A", a.Title);
    }

    [TestMethod]
    public void EditThenUndoToSavedState_ClearsDirty()
    {
        var p = Fresh(title: "Service", filePath: Path.Combine(_tempDir, "p.xml"));
        Logo(p, "A");
        PlaylistDocumentService.SaveDocument(p);

        p.Title = "Service (edited)";
        Assert.IsTrue(p.DirtyTracker.Refresh());

        p.UndoStack.Undo();
        Assert.IsFalse(p.DirtyTracker.Refresh(), "back to exactly the saved state");
    }

    // ---------------- rename -> delete -> undo delete -> undo rename, per item type ----------------

    private static void RenameDeleteUndoUndo(PlaylistInstance p, Item item, string oldTitle)
    {
        p.UndoStack.Clear();
        item.Title = "Renamed";
        p.UndoStack.Execute(new RemovePlaylistItemAction(p, item));
        Assert.AreEqual(0, p.Items.Count);

        Assert.IsTrue(p.UndoStack.Undo()); // undo delete
        Assert.AreEqual(1, p.Items.Count);
        Assert.AreEqual("Renamed", p.Items[0].Title);
        Assert.IsTrue(p.UndoStack.CanUndo, "the rename must still be on the stack");
        Assert.AreEqual("Undo Rename", p.UndoStack.UndoMenuText);

        Assert.IsTrue(p.UndoStack.Undo()); // undo rename
        Assert.AreEqual(oldTitle, p.Items[0].Title);
    }

    [TestMethod]
    public void RenameDeleteUndoUndo_SectionHeading()
    {
        var p = new PlaylistInstance();
        var item = new SectionHeadingItem { Title = "Welcome" };
        p.Items.Add(item);
        RenameDeleteUndoUndo(p, item, "Welcome");
    }

    [TestMethod]
    public void RenameDeleteUndoUndo_Comment()
    {
        var p = new PlaylistInstance();
        var item = new CommentItem { Title = "Note" };
        p.Items.Add(item);
        RenameDeleteUndoUndo(p, item, "Note");
    }

    [TestMethod]
    public void RenameDeleteUndoUndo_MediaGroup()
    {
        var p = new PlaylistInstance();
        var item = new MediaGroupItemInstance(p) { Title = "Photos" };
        p.Items.Add(item);
        RenameDeleteUndoUndo(p, item, "Photos");
    }

    // ---------------- per-item theme picker ----------------

    [TestMethod]
    public void ScriptureItemTheme_IsUndoable()
    {
        var p = new PlaylistInstance();
        var scripture = new ScriptureItemInstance(p)
        {
            Title = "John 3:16", Book = "JHN", Translation = "BSB",
            StartChapter = 3, StartVerse = 16, EndChapter = 3, EndVerse = 16
        };
        p.Items.Add(scripture);
        p.UndoStack.Clear();

        var theme = Guid.NewGuid();
        scripture.Design = theme;
        Assert.AreEqual("Undo Change Theme", p.UndoStack.UndoMenuText);
        p.UndoStack.Undo();
        Assert.AreEqual(Guid.Empty, scripture.Design);
        p.UndoStack.Redo();
        Assert.AreEqual(theme, scripture.Design);
    }

    // ---------------- scripture reference ----------------

    [TestMethod]
    public void ScriptureReferenceEdit_UndoRestoresAllFields_AndTitleIsNotRecordedSeparately()
    {
        var p = new PlaylistInstance();
        var scripture = new ScriptureItemInstance(p)
        {
            Title = "John 3:16", Book = "JHN", Translation = "BSB",
            StartChapter = 3, StartVerse = 16, EndChapter = 3, EndVerse = 16
        };
        p.Items.Add(scripture);
        p.UndoStack.Clear();

        var edited = new ScriptureReference("ROM", 8, 28, 8, 30, "KJV", "Romans 8:28-30");
        p.UndoStack.Execute(new EditScriptureReferenceAction(p, scripture, edited));

        Assert.AreEqual("ROM", scripture.Book);
        Assert.AreEqual("Romans 8:28-30", scripture.Title);
        p.UndoStack.Undo();
        Assert.AreEqual("JHN", scripture.Book);
        Assert.AreEqual(3, scripture.StartChapter);
        Assert.AreEqual(16, scripture.StartVerse);
        Assert.AreEqual("BSB", scripture.Translation);
        Assert.AreEqual("John 3:16", scripture.Title);
        Assert.IsFalse(p.UndoStack.CanUndo, "one step, not one per field");

        p.UndoStack.Redo();
        Assert.AreEqual("ROM", scripture.Book);
        Assert.AreEqual(30, scripture.EndVerse);
    }
}
