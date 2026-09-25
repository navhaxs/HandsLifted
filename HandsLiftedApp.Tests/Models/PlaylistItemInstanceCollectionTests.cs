using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class PlaylistItemInstanceCollectionTests
{
    // Minimal INotifyPropertyChanged + IDisposable test double - deliberately not a real
    // SongItemInstance, since the behavior under test (Move must not call Dispose) lives entirely
    // in PlaylistItemInstanceCollection<T>.OnCollectionChanged and doesn't need the full
    // SongLibraryIndex/dispatcher machinery to exercise.
    private sealed class DisposableItem : INotifyPropertyChanged, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Dispose() => IsDisposed = true;
    }

    [TestMethod]
    public void Move_DoesNotDisposeItem()
    {
        var item = new DisposableItem();
        var other = new DisposableItem();
        var collection = new PlaylistItemInstanceCollection<DisposableItem>(new List<DisposableItem> { item, other });

        // ObservableCollection<T>.Move raises a single NotifyCollectionChangedAction.Move event
        // whose OldItems and NewItems both contain the SAME item - it never left the collection,
        // it just changed position (this is what "Move Up"/"Move Down" and drag-reorder use).
        collection.Move(0, 1);

        Assert.IsFalse(item.IsDisposed,
            "Move-ing an item within the collection must not dispose it - the item is still live in the playlist, it only changed position");
    }

    [TestMethod]
    public void Remove_DisposesItem()
    {
        var item = new DisposableItem();
        var collection = new PlaylistItemInstanceCollection<DisposableItem>(new List<DisposableItem> { item });

        collection.Remove(item);

        Assert.IsTrue(item.IsDisposed,
            "Removing an item from the collection must still dispose it - only Move is exempt");
    }

    // Regression test: MotionBackgroundVideoOverride must be marked [DataField] so that setting or
    // clearing it marks the playlist dirty (via HandleDataFieldPropertyChanges -> ItemDataModified),
    // the same way Item.SlideTransitionDurationMs already is - otherwise the override is silently
    // lost on exit/autosave because nothing ever flags the playlist as having unsaved changes.
    [TestMethod]
    public void SettingMotionBackgroundVideoOverride_RaisesItemDataModified()
    {
        var song = new SongItem { Title = "Amazing Grace" };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        var instance = new SongItemInstance(null) { SongId = song.UUID };
        var collection = new PlaylistItemInstanceCollection<SongItemInstance>(new List<SongItemInstance> { instance });

        var raised = false;
        collection.ItemDataModified += (_, _) => raised = true;

        instance.MotionBackgroundVideoOverride = @"C:\Videos\override.mp4";

        Assert.IsTrue(raised,
            "Setting MotionBackgroundVideoOverride must mark the playlist item collection dirty, " +
            "otherwise the override is silently lost on exit since nothing triggers autosave/unsaved-changes tracking");
    }
}
