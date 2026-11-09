using System;
using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// How an item is captured and rebuilt so a removed item can come back.
    ///
    /// Removal can't simply be reversed by re-inserting the same instance: the collection disposes items it
    /// drops (a disposed <c>SongItemInstance</c> has torn down its subscriptions for good). So undo rebuilds
    /// a <em>fresh</em> instance from a snapshot - the same saved-file form that Duplicate and save/load use -
    /// which keeps the item's <see cref="Item.UUID"/>.
    ///
    /// Consequence: a restored item is what you would get by saving and reloading the playlist. State that
    /// isn't persisted (generated slides, selected slide within the item) is regenerated, not carried over.
    /// </summary>
    internal static class PlaylistItemSnapshot
    {
        /// <summary>Detached copy of the item's persisted state, taken now (so it includes edits made since it was added).</summary>
        public static Item Capture(Item item)
        {
            var serialized = HandsLiftedDocXmlSerializer.SerializeItem(item);

            // SerializeItem builds a new data object for most types, but returns plain data items (section
            // headings, comments) as themselves, and shares a few inner objects with the live item. Round-trip
            // through XML so the snapshot is fully detached. Clone() assigns a new UUID, so put the real one back.
            var copy = serialized.Clone();
            copy.UUID = item.UUID;
            return copy;
        }

        /// <summary>A brand-new runtime item for <paramref name="snapshot"/>. The snapshot stays pristine and reusable.</summary>
        public static Item Restore(Item snapshot, PlaylistInstance playlist)
        {
            var copy = snapshot.Clone();
            copy.UUID = snapshot.UUID;
            return ItemInstanceFactory.ToItemInstance(copy, playlist);
        }

        public static Item FindOrThrow(PlaylistInstance playlist, Guid itemId) =>
            playlist.Items.FirstOrDefault(i => i.UUID == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} is no longer in the playlist");

        /// <summary>Inserts at <paramref name="index"/> (clamped), leaving whatever was selected still selected.</summary>
        public static void InsertKeepingSelection(PlaylistInstance playlist, Item item, int index)
        {
            var selected = playlist.SelectedItem;
            playlist.Items.Insert(Math.Clamp(index, 0, playlist.Items.Count), item);
            if (selected != null)
            {
                playlist.SelectedItemIndex = playlist.Items.IndexOf(selected);
            }
        }

        /// <summary>Removes the item, leaving the previous selection selected (or none if it was the removed item).</summary>
        public static void RemoveKeepingSelection(PlaylistInstance playlist, Item item)
        {
            var selected = playlist.SelectedItem;
            playlist.Items.RemoveAt(playlist.Items.IndexOf(item));
            if (selected != null)
            {
                playlist.SelectedItemIndex = playlist.Items.IndexOf(selected);
            }
        }
    }

    /// <summary>
    /// Adds an item to the playlist (Add Item dialog, Duplicate, files dropped onto the playlist).
    /// The first <see cref="Do"/> inserts the instance the caller already built (it may already have async
    /// work such as scripture or online-video loading in flight); undo removes it by UUID, and redo rebuilds it
    /// from a snapshot taken at removal time.
    /// </summary>
    public sealed class InsertPlaylistItemAction : IUndoableAction
    {
        private readonly PlaylistInstance _playlist;
        private readonly Guid _itemId;
        private readonly int _index;
        private Item? _initialInstance;
        private Item? _snapshot;

        public InsertPlaylistItemAction(PlaylistInstance playlist, Item item, int index, string description = "Add Item")
        {
            _playlist = playlist;
            _itemId = item.UUID;
            _index = index;
            _initialInstance = item;
            Description = description;
        }

        public string Description { get; }

        public void Do()
        {
            Item item;
            if (_initialInstance != null)
            {
                item = _initialInstance;
                _initialInstance = null; // from here on the playlist owns it; redo rebuilds from a snapshot
            }
            else
            {
                item = PlaylistItemSnapshot.Restore(_snapshot!, _playlist);
            }

            PlaylistItemSnapshot.InsertKeepingSelection(_playlist, item, _index);
        }

        public void Undo()
        {
            var item = PlaylistItemSnapshot.FindOrThrow(_playlist, _itemId);
            _snapshot = PlaylistItemSnapshot.Capture(item);
            PlaylistItemSnapshot.RemoveKeepingSelection(_playlist, item);
        }
    }

    /// <summary>Deletes an item from the playlist. Undo rebuilds it at the same position from a snapshot.</summary>
    public sealed class RemovePlaylistItemAction : IUndoableAction
    {
        private readonly PlaylistInstance _playlist;
        private readonly Guid _itemId;
        private int _index;
        private bool _wasSelected;
        private Item? _snapshot;

        public RemovePlaylistItemAction(PlaylistInstance playlist, Item item, string description = "Delete Item")
        {
            _playlist = playlist;
            _itemId = item.UUID;
            Description = description;
        }

        public string Description { get; }

        public void Do()
        {
            var item = PlaylistItemSnapshot.FindOrThrow(_playlist, _itemId);
            _index = _playlist.Items.IndexOf(item);
            _wasSelected = ReferenceEquals(_playlist.SelectedItem, item);
            _snapshot = PlaylistItemSnapshot.Capture(item); // fresh each time: includes edits made since last restore
            PlaylistItemSnapshot.RemoveKeepingSelection(_playlist, item);
        }

        public void Undo()
        {
            var restored = PlaylistItemSnapshot.Restore(_snapshot!, _playlist);
            PlaylistItemSnapshot.InsertKeepingSelection(_playlist, restored, _index);

            // if the deleted item was the selected one, bringing it back should bring the selection back too
            if (_wasSelected)
            {
                _playlist.SelectedItemIndex = _playlist.Items.IndexOf(restored);
            }
        }
    }
}
