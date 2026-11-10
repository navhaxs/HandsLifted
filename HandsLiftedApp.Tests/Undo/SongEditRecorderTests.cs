using System;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class SongEditRecorderTests
{
    [TestInitialize]
    public void Setup()
    {
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    private sealed class Editor
    {
        public required SongItemInstance Song { get; init; }
        public required UndoStack Stack { get; init; }
        public required SongEditRecorder Recorder { get; init; }
    }

    /// <summary>A library-backed song with two sections, as the song editor sees it, with an empty history.</summary>
    private static Editor Open(bool timeControlled = false, Func<DateTime>? now = null)
    {
        var data = new SongItem { Title = "Amazing Grace", Copyright = "Public domain" };
        var verse = new SongStanza(Guid.NewGuid(), "Verse 1", "Amazing grace");
        var chorus = new SongStanza(Guid.NewGuid(), "Chorus", "How sweet the sound");
        data.Stanzas.Add(verse);
        data.Stanzas.Add(chorus);
        data.Arrangement.Add(verse.Id);
        data.Arrangement.Add(chorus.Id);
        Globals.Instance.SongLibraryIndex.Register(data, "irrelevant.xml", "irrelevant");

        var playlist = new PlaylistInstance();
        var song = new SongItemInstance(playlist) { SongId = data.UUID };

        var stack = timeControlled ? new UndoStack(now!, TimeSpan.FromSeconds(1.5)) : new UndoStack();
        var recorder = new SongEditRecorder(stack, song);
        stack.Clear();
        return new Editor { Song = song, Stack = stack, Recorder = recorder };
    }

    [TestMethod]
    public void TitleEdit_UndoRedo()
    {
        var e = Open();
        e.Song.Title = "How Great Thou Art";

        Assert.AreEqual("Undo Edit Title", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.AreEqual("Amazing Grace", e.Song.Title);
        Assert.IsFalse(e.Stack.CanUndo);
        e.Stack.Redo();
        Assert.AreEqual("How Great Thou Art", e.Song.Title);
    }

    [TestMethod]
    public void TypingMergesIntoOneStep_ButAPauseStartsANewOne()
    {
        var clock = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var e = Open(timeControlled: true, now: () => clock);

        e.Song.Title = "H";
        clock = clock.AddMilliseconds(200);
        e.Song.Title = "Ho";
        clock = clock.AddMilliseconds(200);
        e.Song.Title = "How";
        clock = clock.AddSeconds(5); // a pause
        e.Song.Title = "How Great";

        e.Stack.Undo();
        Assert.AreEqual("How", e.Song.Title);
        e.Stack.Undo();
        Assert.AreEqual("Amazing Grace", e.Song.Title);
        Assert.IsFalse(e.Stack.CanUndo);
    }

    [TestMethod]
    public void LyricsEdit_IsUndoable_AndNamedSo()
    {
        var e = Open();
        e.Song.Stanzas[0].Lyrics = "Amazing grace, how sweet";

        Assert.AreEqual("Undo Edit Lyrics", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.AreEqual("Amazing grace", e.Song.Stanzas[0].Lyrics);
    }

    [TestMethod]
    public void RenamingASection_IsUndoable()
    {
        var e = Open();
        e.Song.Stanzas[1].Name = "Refrain";

        Assert.AreEqual("Undo Rename Section", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.AreEqual("Chorus", e.Song.Stanzas[1].Name);
    }

    [TestMethod]
    public void AddingASection_UndoRemovesIt_RedoBringsItBackWithTheSameId()
    {
        var e = Open();
        var added = new SongStanza(Guid.NewGuid(), "Bridge", "Was blind but now I see");
        e.Song.Stanzas.Add(added);

        Assert.AreEqual("Undo Add Section", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.AreEqual(2, e.Song.Stanzas.Count);
        e.Stack.Redo();
        Assert.AreEqual(3, e.Song.Stanzas.Count);
        Assert.AreEqual(added.Id, e.Song.Stanzas[2].Id);
        Assert.AreEqual("Was blind but now I see", e.Song.Stanzas[2].Lyrics);
    }

    [TestMethod]
    public void DeletingASection_UndoRestoresItAndItsPlaceInTheArrangement()
    {
        var e = Open();
        var verse = e.Song.Stanzas[0];
        var id = verse.Id;

        using (e.Recorder.BeginBatch("Delete Section"))
        {
            e.Song.Stanzas.Remove(verse);
            e.Song.Arrangement.Remove(id);
        }

        Assert.AreEqual(1, e.Song.Stanzas.Count);
        e.Stack.Undo();

        Assert.AreEqual(2, e.Song.Stanzas.Count);
        Assert.AreEqual(id, e.Song.Stanzas[0].Id);
        Assert.AreEqual("Amazing grace", e.Song.Stanzas[0].Lyrics);
        Assert.AreEqual(id, e.Song.Arrangement[0]);
        Assert.AreEqual(2, e.Song.Arrangement.Count);
    }

    [TestMethod]
    public void ReorderingSections_UndoRestoresTheOrder()
    {
        var e = Open();
        var first = e.Song.Stanzas[0].Id;
        e.Song.Stanzas.Move(0, 1);

        Assert.AreEqual("Undo Reorder Sections", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.AreEqual(first, e.Song.Stanzas[0].Id);
    }

    [TestMethod]
    public void ArrangementEdit_IsUndoable_EvenWhenTheCollectionIsReplaced()
    {
        var e = Open();
        var original = e.Song.Arrangement.ToList();

        e.Song.ResetArrangement(); // replaces the collection object
        e.Song.Arrangement.RemoveAt(0);

        e.Stack.Undo();
        e.Stack.Undo();
        CollectionAssert.AreEqual(original, e.Song.Arrangement.ToList());
    }

    [TestMethod]
    public void ABatchIsOneStep_WithItsOwnName()
    {
        var e = Open();

        using (e.Recorder.BeginBatch("Apply Lyrics Text"))
        {
            e.Song.Title = "Imported";
            e.Song.Copyright = "CCLI";
            e.Song.Stanzas[0].Lyrics = "new";
            e.Song.Stanzas.RemoveAt(1);
        }

        Assert.AreEqual("Undo Apply Lyrics Text", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.IsFalse(e.Stack.CanUndo, "one step for the whole batch");
        Assert.AreEqual("Amazing Grace", e.Song.Title);
        Assert.AreEqual("Public domain", e.Song.Copyright);
        Assert.AreEqual("Amazing grace", e.Song.Stanzas[0].Lyrics);
        Assert.AreEqual(2, e.Song.Stanzas.Count);

        e.Stack.Redo();
        Assert.AreEqual("Imported", e.Song.Title);
        Assert.AreEqual(1, e.Song.Stanzas.Count);
    }

    [TestMethod]
    public void ThemeAndOptions_AreUndoable()
    {
        var e = Open();
        var theme = Guid.NewGuid();

        e.Song.Design = theme;
        Assert.AreEqual("Undo Change Theme", e.Stack.UndoMenuText);

        e.Song.EndOnBlankSlide = false;
        Assert.AreEqual("Undo Change Song Options", e.Stack.UndoMenuText);

        e.Stack.Undo();
        Assert.IsTrue(e.Song.EndOnBlankSlide);
        e.Stack.Undo();
        Assert.AreEqual(Guid.Empty, e.Song.Design);
    }

    [TestMethod]
    public void MotionBackgroundVideo_IsUndoable()
    {
        var e = Open();
        e.Song.MotionBackgroundVideoPath = @"C:\Videos\bg.mp4";
        Assert.AreEqual("Undo Change Background Video", e.Stack.UndoMenuText);
        e.Stack.Undo();
        Assert.IsNull(e.Song.MotionBackgroundVideoPath);
    }

    [TestMethod]
    public void UndoItself_IsNotRecorded_AndLaterEditsStillDiffCorrectly()
    {
        var e = Open();
        e.Song.Title = "A";
        e.Stack.Undo();
        Assert.IsFalse(e.Stack.CanUndo);
        Assert.IsTrue(e.Stack.CanRedo);

        e.Song.Title = "B";
        Assert.IsFalse(e.Stack.CanRedo);
        e.Stack.Undo();
        Assert.AreEqual("Amazing Grace", e.Song.Title);
    }

    [TestMethod]
    public void Suppress_KeepsProgrammaticChangesOutOfHistory()
    {
        var e = Open();
        using (e.Recorder.Suppress())
        {
            e.Song.Title = "Set by code";
        }

        Assert.IsFalse(e.Stack.CanUndo);
        e.Song.Title = "Typed";
        e.Stack.Undo();
        Assert.AreEqual("Set by code", e.Song.Title);
    }

    [TestMethod]
    public void Retarget_FollowsANewSong_WithoutRecordingTheSwitch()
    {
        var e = Open();
        var draft = SongItemInstance.NewDraft(new PlaylistInstance());
        draft.Title = "Draft";

        e.Recorder.Retarget(draft);
        Assert.IsFalse(e.Stack.CanUndo);

        draft.Title = "Draft 2";
        Assert.IsTrue(e.Stack.CanUndo);
        e.Stack.Undo();
        Assert.AreEqual("Draft", draft.Title);
    }

    [TestMethod]
    public void AnUnsavedDraftSong_IsUndoable()
    {
        var draft = SongItemInstance.NewDraft(new PlaylistInstance());
        var stack = new UndoStack();
        var recorder = new SongEditRecorder(stack, draft);

        draft.Title = "New song";
        draft.Stanzas.Add(new SongStanza(Guid.NewGuid(), "Verse 1", "la la"));
        Assert.AreEqual(1, draft.Stanzas.Count);

        stack.Undo();
        Assert.AreEqual(0, draft.Stanzas.Count);
        stack.Undo();
        Assert.AreEqual("", draft.Title);
        recorder.Dispose();
    }

    [TestMethod]
    public void Dispose_StopsRecording()
    {
        var e = Open();
        e.Recorder.Dispose();
        e.Song.Title = "After close";
        Assert.IsFalse(e.Stack.CanUndo);
    }
}
