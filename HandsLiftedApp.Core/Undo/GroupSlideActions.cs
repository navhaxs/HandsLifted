using System;
using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Entry points for editing the slides (<see cref="MediaGroupItem.GroupItem"/>s) inside a media group.
    ///
    /// If the group is a live item of a playlist, each edit goes through that playlist's undo stack. Otherwise
    /// (e.g. the standalone slide editor, which has no playlist) the edit is simply applied, with no history.
    ///
    /// Edits are located by <em>index</em>, and the group by the playlist item's UUID. That is safe because the
    /// history is linear: when an action is undone or redone, the group is in exactly the state right after that
    /// action ran. It also means the actions keep working after the whole group has been deleted and brought back
    /// by undo (which rebuilds the group instance - and its slide objects - from a snapshot, keeping its UUID).
    /// </summary>
    public static class GroupSlideEdits
    {
        /// <summary>The playlist this group is currently an item of, or null if it isn't in one.</summary>
        public static PlaylistInstance? PlaylistOf(MediaGroupItem group)
        {
            var playlist = (group as IItemInstance)?.ParentPlaylist;
            return playlist != null && playlist.Items.Contains(group) ? playlist : null;
        }

        /// <summary>Groups several slide edits into one undo step. A no-op for a group that isn't in a playlist.</summary>
        public static IDisposable BeginTransaction(MediaGroupItem group, string description) =>
            PlaylistOf(group)?.UndoStack.BeginTransaction(description) ?? NoopScope.Instance;

        /// <param name="regenerate">
        /// Whether the group's generated slides are rebuilt straight away. The editor windows pass false, as they
        /// rebuild on "Apply". Undo and redo always rebuild.
        /// </param>
        public static void Insert(MediaGroupItem group, int index, MediaGroupItem.GroupItem item,
            string description = "Add Slide", bool regenerate = true) =>
            Run(group, new InsertGroupSlideAction(group, index, item, description, regenerate));

        public static void RemoveAt(MediaGroupItem group, int index, string description = "Delete Slide",
            bool regenerate = true) =>
            Run(group, new RemoveGroupSlideAction(group, index, description, regenerate));

        public static void Move(MediaGroupItem group, int fromIndex, int toIndex, string description = "Move Slide",
            bool regenerate = true)
        {
            if (fromIndex == toIndex) return;
            Run(group, new MoveGroupSlideAction(group, fromIndex, toIndex, description, regenerate));
        }

        public static void SetMediaPath(MediaGroupItem group, int index, string? newPath,
            string description = "Change Media", bool regenerate = true) =>
            Run(group, new SetGroupSlideMediaPathAction(group, index, newPath, description, regenerate));

        private static void Run(MediaGroupItem group, IUndoableAction action)
        {
            var playlist = PlaylistOf(group);
            if (playlist != null) playlist.UndoStack.Execute(action);
            else action.Do();
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }

    internal abstract class GroupSlideAction : IUndoableAction
    {
        private readonly Guid _groupId;
        private readonly MediaGroupItem _fallbackGroup;
        private readonly PlaylistInstance? _playlist;
        private readonly bool _regenerateOnFirstDo;
        private bool _hasRun;

        protected GroupSlideAction(MediaGroupItem group, string description, bool regenerate)
        {
            _groupId = group.UUID;
            _fallbackGroup = group;
            _playlist = GroupSlideEdits.PlaylistOf(group);
            _regenerateOnFirstDo = regenerate;
            Description = description;
        }

        public string Description { get; }

        public void Do()
        {
            var group = Resolve();
            var regenerate = _hasRun || _regenerateOnFirstDo; // redo always rebuilds
            _hasRun = true;
            Apply(group, () => DoCore(group), regenerate);
            AfterDo(group);
        }

        public void Undo()
        {
            var group = Resolve();
            Apply(group, () => UndoCore(group), regenerate: true);
            AfterUndo(group);
        }

        protected abstract void DoCore(MediaGroupItem group);
        protected abstract void UndoCore(MediaGroupItem group);
        protected virtual void AfterDo(MediaGroupItem group) { }
        protected virtual void AfterUndo(MediaGroupItem group) { }

        /// <summary>The live group: looked up in the playlist (it may have been rebuilt since), else the original.</summary>
        private MediaGroupItem Resolve()
        {
            if (_playlist == null) return _fallbackGroup; // not part of a playlist: nothing to look up

            return _playlist.Items.FirstOrDefault(i => i.UUID == _groupId) as MediaGroupItem
                   ?? throw new InvalidOperationException($"Group {_groupId} is no longer in the playlist");
        }

        protected static MediaGroupItem.GroupItem ItemAt(MediaGroupItem group, int index)
        {
            if (index < 0 || index >= group.Items.Count)
                throw new InvalidOperationException($"No slide at index {index} (group has {group.Items.Count})");
            return group.Items[index];
        }

        protected static IItemInstance? InstanceOf(MediaGroupItem group) => group as IItemInstance;

        /// <summary>
        /// Runs <paramref name="edit"/> and rebuilds the generated slides, keeping the group's selected slide
        /// selected (by reference - an index would drift as slides come and go).
        /// </summary>
        private static void Apply(MediaGroupItem group, Action edit, bool regenerate)
        {
            var instance = InstanceOf(group);
            var previouslySelected = instance?.Slides.ElementAtOrDefault(instance.SelectedSlideIndex);

            edit();

            if (!regenerate) return;
            Regenerate(group);

            if (instance != null && previouslySelected != null)
            {
                var index = instance.Slides.IndexOf(previouslySelected);
                if (index >= 0) instance.SelectedSlideIndex = index;
            }
        }

        internal static void Regenerate(MediaGroupItem group)
        {
            switch (group)
            {
                case MediaGroupItemInstance m: m.GenerateSlides(); break;
                case PowerPointPresentationItemInstance p: p.GenerateSlides(); break;
                case PDFSlidesGroupItemInstance d: d.GenerateSlides(); break;
            }
        }
    }

    /// <summary>Adds a slide to a group at an index (add, duplicate, files dropped in, move into this group).</summary>
    internal sealed class InsertGroupSlideAction : GroupSlideAction
    {
        private readonly int _requestedIndex;
        private int _index;
        private MediaGroupItem.GroupItem _item;

        public InsertGroupSlideAction(MediaGroupItem group, int index, MediaGroupItem.GroupItem item,
            string description, bool regenerate) : base(group, description, regenerate)
        {
            _requestedIndex = index;
            _item = item;
        }

        protected override void DoCore(MediaGroupItem group)
        {
            _index = Math.Clamp(_requestedIndex, 0, group.Items.Count);
            group.Items.Insert(_index, _item);
        }

        protected override void UndoCore(MediaGroupItem group)
        {
            // hold on to whatever is actually there now (the group may have been rebuilt since), for redo
            _item = ItemAt(group, _index);
            group.Items.RemoveAt(_index);
        }
    }

    /// <summary>Removes a slide from a group. Undo puts the same slide back at the same position.</summary>
    internal sealed class RemoveGroupSlideAction : GroupSlideAction
    {
        private readonly int _index;
        private MediaGroupItem.GroupItem? _removed;
        private bool _wasSelected;

        public RemoveGroupSlideAction(MediaGroupItem group, int index, string description, bool regenerate)
            : base(group, description, regenerate)
        {
            _index = index;
        }

        protected override void DoCore(MediaGroupItem group)
        {
            _removed = ItemAt(group, _index);
            _wasSelected = InstanceOf(group)?.SelectedSlideIndex == _index;
            group.Items.RemoveAt(_index);
        }

        protected override void UndoCore(MediaGroupItem group) => group.Items.Insert(_index, _removed!);

        protected override void AfterUndo(MediaGroupItem group)
        {
            // if the deleted slide was the selected one, bringing it back should bring the selection back too
            var instance = InstanceOf(group);
            if (_wasSelected && instance != null) instance.SelectedSlideIndex = _index;
        }
    }

    /// <summary>Reorders a slide within a group.</summary>
    internal sealed class MoveGroupSlideAction : GroupSlideAction
    {
        private readonly int _from;
        private readonly int _to;

        public MoveGroupSlideAction(MediaGroupItem group, int from, int to, string description, bool regenerate)
            : base(group, description, regenerate)
        {
            _from = from;
            _to = to;
        }

        protected override void DoCore(MediaGroupItem group)
        {
            ItemAt(group, _from);
            ItemAt(group, _to);
            group.Items.Move(_from, _to);
        }

        protected override void UndoCore(MediaGroupItem group) => group.Items.Move(_to, _from);
    }

    /// <summary>Points a media slide at a different file. (A file copied into the media library stays there on undo.)</summary>
    internal sealed class SetGroupSlideMediaPathAction : GroupSlideAction
    {
        private readonly int _index;
        private readonly string? _newPath;
        private string? _oldPath;

        public SetGroupSlideMediaPathAction(MediaGroupItem group, int index, string? newPath, string description,
            bool regenerate) : base(group, description, regenerate)
        {
            _index = index;
            _newPath = newPath;
        }

        private static MediaGroupItem.MediaItem MediaItemAt(MediaGroupItem group, int index) =>
            ItemAt(group, index) as MediaGroupItem.MediaItem
            ?? throw new InvalidOperationException($"Slide {index} is not a media slide");

        protected override void DoCore(MediaGroupItem group)
        {
            var item = MediaItemAt(group, _index);
            _oldPath = item.SourceMediaFilePath;
            item.SourceMediaFilePath = _newPath;
        }

        protected override void UndoCore(MediaGroupItem group) =>
            MediaItemAt(group, _index).SourceMediaFilePath = _oldPath;
    }
}
