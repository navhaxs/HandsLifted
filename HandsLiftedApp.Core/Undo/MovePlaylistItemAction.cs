using System;
using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Moves one item within the playlist's item list (Move Up/Down buttons, drag-reorder).
    /// The item is tracked by <see cref="Item.UUID"/>, not by index or reference, so the action stays valid
    /// when other edits shift positions or the instance is rebuilt.
    /// </summary>
    public sealed class MovePlaylistItemAction : IUndoableAction
    {
        private readonly PlaylistInstance _playlist;
        private readonly Guid _itemId;
        private readonly int _fromIndex;
        private readonly int _toIndex;

        public MovePlaylistItemAction(PlaylistInstance playlist, Item item, int fromIndex, int toIndex,
            string description = "Move Item")
        {
            _playlist = playlist;
            _itemId = item.UUID;
            _fromIndex = fromIndex;
            _toIndex = toIndex;
            Description = description;
        }

        public string Description { get; }

        public void Do() => MoveTo(_toIndex);

        public void Undo() => MoveTo(_fromIndex);

        private void MoveTo(int targetIndex)
        {
            var item = _playlist.Items.FirstOrDefault(i => i.UUID == _itemId)
                       ?? throw new InvalidOperationException($"Item {_itemId} is no longer in the playlist");

            var current = _playlist.Items.IndexOf(item);
            if (current == targetIndex) return;

            // Keep whatever was selected selected (by reference), as the original move handlers did:
            // reordering must not change the selection.
            var selected = _playlist.SelectedItem;
            _playlist.Items.Move(current, targetIndex);
            if (selected != null)
            {
                _playlist.SelectedItemIndex = _playlist.Items.IndexOf(selected);
            }
        }
    }
}
