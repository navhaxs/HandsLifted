using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models;

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
}
