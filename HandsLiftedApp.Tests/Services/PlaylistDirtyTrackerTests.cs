using System;
using System.IO;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class PlaylistDirtyTrackerTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistDirtyTrackerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string PathFor(string name) => Path.Combine(_tempDir, name);

    [TestMethod]
    public void NewPlaylist_IsNotDirty()
    {
        var playlist = new PlaylistInstance();

        Assert.IsFalse(playlist.IsDirty, "constructing a playlist is not an edit");
    }

    [TestMethod]
    public void Untitled_DirtyOnlyWhileNonEmpty()
    {
        var playlist = new PlaylistInstance();
        playlist.DirtyTracker.MarkPristine();

        playlist.Title = "Sunday";
        Assert.IsTrue(playlist.DirtyTracker.Refresh());

        playlist.Title = "";
        Assert.IsFalse(playlist.DirtyTracker.Refresh());
        Assert.IsFalse(playlist.IsDirty);
    }

    [TestMethod]
    public void EditThenRevert_ReturnsToClean()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.SaveDocument(playlist);
        Assert.IsFalse(playlist.IsDirty);

        playlist.Title = "v2";
        Assert.IsTrue(playlist.IsDirty, "the change event marks dirty optimistically");
        Assert.IsTrue(playlist.DirtyTracker.Refresh());

        playlist.Title = "v1"; // e.g. an undo
        Assert.IsFalse(playlist.DirtyTracker.Refresh());
        Assert.IsFalse(playlist.IsDirty);
    }

    [TestMethod]
    public void Refresh_CatchesChangeWhoseEventWasMissed()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.SaveDocument(playlist);

        playlist.Title = "v2";
        playlist.IsDirty = false; // simulate the change event never having been raised

        Assert.IsTrue(playlist.DirtyTracker.Refresh());
        Assert.IsTrue(playlist.IsDirty);
    }

    [TestMethod]
    public void Refresh_ClearsFlagThatWasSetByAnEventWithNoRealChange()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.SaveDocument(playlist);

        playlist.IsDirty = true; // e.g. an event fired for something that isn't serialized

        Assert.IsFalse(playlist.DirtyTracker.Refresh());
    }

    [TestMethod]
    public void SaveDocument_MarksClean()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        playlist.Title = "v2";
        Assert.IsTrue(playlist.IsDirty);

        PlaylistDocumentService.SaveDocument(playlist);

        Assert.IsFalse(playlist.IsDirty);
        Assert.IsFalse(playlist.DirtyTracker.Refresh());
    }

    [TestMethod]
    public void MarkLoaded_Clean_WhenNoResaveNeeded()
    {
        var playlist = new PlaylistInstance { Title = "loaded", PlaylistFilePath = PathFor("p.xml") };

        playlist.DirtyTracker.MarkLoaded(needsResave: false);

        Assert.IsFalse(playlist.IsDirty);
        Assert.IsFalse(playlist.DirtyTracker.Refresh());

        playlist.Title = "edited";
        Assert.IsTrue(playlist.DirtyTracker.Refresh());
    }

    [TestMethod]
    public void MarkLoaded_NeedsResave_StaysDirtyUntilSaved()
    {
        var playlist = new PlaylistInstance { Title = "migrated", PlaylistFilePath = PathFor("p.xml") };

        playlist.DirtyTracker.MarkLoaded(needsResave: true);
        Assert.IsTrue(playlist.IsDirty);
        Assert.IsTrue(playlist.DirtyTracker.Refresh(), "content hasn't changed but the file still needs rewriting");

        PlaylistDocumentService.SaveDocument(playlist);
        Assert.IsFalse(playlist.DirtyTracker.Refresh());
    }

    [TestMethod]
    public void Refresh_WhileLoading_LeavesFlagAlone()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.SaveDocument(playlist);
        playlist.Title = "v2";
        playlist.IsPlaylistLoading = true;
        playlist.IsDirty = false;

        playlist.DirtyTracker.Refresh();

        Assert.IsFalse(playlist.IsDirty, "a half-built playlist must not be judged");
    }

    [TestMethod]
    public void AutoSave_Off_StillTracksDirty()
    {
        // The tracker never consults auto-save: saving via the manual path and editing is enough.
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.SaveDocument(playlist);

        playlist.Title = "v2";
        playlist.IsDirty = false; // missed event

        Assert.IsTrue(playlist.DirtyTracker.Refresh());
    }

    [TestMethod]
    public void AutoSave_ClearsDirtyAndRebaselines()
    {
        var playlist = new PlaylistInstance { Title = "v1", PlaylistFilePath = PathFor("p.xml") };
        PlaylistDocumentService.AutoSaveToFile(playlist);

        playlist.Title = "v2";
        Assert.IsTrue(playlist.DirtyTracker.Refresh());

        PlaylistDocumentService.AutoSaveToFile(playlist);
        Assert.IsFalse(playlist.IsDirty);
        Assert.IsFalse(playlist.DirtyTracker.Refresh());
    }

    // --- G1: slide edits inside a media group must raise ItemDataModified ---

    [TestMethod]
    public void MediaGroup_AddingSlide_RaisesItemDataModified()
    {
        var playlist = new PlaylistInstance();
        var group = new MediaGroupItemInstance(playlist) { Title = "Photos" };
        var raised = 0;
        group.ItemDataModified += (_, _) => raised++;

        group.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });

        Assert.IsTrue(raised > 0, "in-place Items mutation must be reported");
    }

    [TestMethod]
    public void MediaGroup_RemovingAndMovingSlides_RaiseItemDataModified()
    {
        var playlist = new PlaylistInstance();
        var group = new MediaGroupItemInstance(playlist) { Title = "Photos" };
        group.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });
        group.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\b.png" });
        var raised = 0;
        group.ItemDataModified += (_, _) => raised++;

        group.Items.Move(0, 1);
        var afterMove = raised;
        group.Items.RemoveAt(0);

        Assert.IsTrue(afterMove > 0, "move");
        Assert.IsTrue(raised > afterMove, "remove");
    }

    [TestMethod]
    public void MediaGroup_SlideEditInsidePlaylist_MarksPlaylistDirty()
    {
        var path = PathFor("p.xml");
        var playlist = new PlaylistInstance { Title = "p", PlaylistFilePath = path };
        var group = new MediaGroupItemInstance(playlist) { Title = "Photos" };
        group.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });
        playlist.Items.Add(group);
        PlaylistDocumentService.SaveDocument(playlist);
        Assert.IsFalse(playlist.IsDirty);

        group.Items.RemoveAt(0);

        Assert.IsTrue(playlist.IsDirty);
    }
}
