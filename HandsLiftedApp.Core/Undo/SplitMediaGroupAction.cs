using System;
using System.Collections.Generic;
using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Splits a media group in two at a slide: the original keeps the slides before it, and a new generic
    /// <see cref="MediaGroupItemInstance"/> (placed straight after the original) takes that slide onwards.
    /// The new group is always generic, even when the original is a PDF / PowerPoint / Google Slides import:
    /// it must not carry the import source, or a later Sync would re-import the whole file into it.
    ///
    /// Undo appends the new group's slides back onto the original and removes the new group. The new group is
    /// found by UUID, and rebuilt on redo (keeping the UUID), as removed playlist items are elsewhere.
    /// </summary>
    public sealed class SplitMediaGroupAction : IUndoableAction
    {
        private readonly PlaylistInstance _playlist;
        private readonly Guid _groupId;
        private readonly Guid _newGroupId = Guid.NewGuid();
        private readonly int _splitIndex;
        private readonly string _newTitle;

        public SplitMediaGroupAction(PlaylistInstance playlist, MediaGroupItem group, int splitIndex,
            string description = "Split Media Group")
        {
            _playlist = playlist;
            _groupId = group.UUID;
            _splitIndex = splitIndex;
            _newTitle = $"{group.Title} (part 2)";
            Description = description;
        }

        public string Description { get; }

        /// <summary>Whether <paramref name="group"/> can be split so that slide <paramref name="itemIndex"/> starts the second half.</summary>
        public static bool CanSplit(MediaGroupItem group, int itemIndex) =>
            group is IItemInstance
            && group is not OnlineVideoItem
            && itemIndex > 0
            && itemIndex < group.Items.Count;

        public void Do()
        {
            var original = (MediaGroupItem)PlaylistItemSnapshot.FindOrThrow(_playlist, _groupId);
            if (_splitIndex <= 0 || _splitIndex >= original.Items.Count)
                throw new InvalidOperationException($"Cannot split group at {_splitIndex} (has {original.Items.Count})");

            var originalIndex = _playlist.Items.IndexOf(original);

            // built before it joins the playlist, so setting these isn't recorded as separate edits
            var tail = original.Items.Skip(_splitIndex).ToList();
            var second = new MediaGroupItemInstance(_playlist)
            {
                UUID = _newGroupId,
                Title = _newTitle,
                IsLooping = original.IsLooping,
                SlideTransitionDurationMs = original.SlideTransitionDurationMs,
            };
            second.AutoAdvanceTimer.IsEnabled = original.AutoAdvanceTimer.IsEnabled;
            second.AutoAdvanceTimer.IntervalMs = original.AutoAdvanceTimer.IntervalMs;
            foreach (var item in tail) second.Items.Add(item);

            for (var i = original.Items.Count - 1; i >= _splitIndex; i--) original.Items.RemoveAt(i);

            Rebuild(original);
            second.GenerateSlides();

            PlaylistItemSnapshot.InsertKeepingSelection(_playlist, second, originalIndex + 1);
        }

        public void Undo()
        {
            var original = (MediaGroupItem)PlaylistItemSnapshot.FindOrThrow(_playlist, _groupId);
            var second = (MediaGroupItem)PlaylistItemSnapshot.FindOrThrow(_playlist, _newGroupId);

            foreach (var item in second.Items.ToList()) original.Items.Add(item);

            PlaylistItemSnapshot.RemoveKeepingSelection(_playlist, second);
            Rebuild(original);
        }

        private static void Rebuild(MediaGroupItem group)
        {
            GroupSlideAction.Regenerate(group);
            if (group is IItemInstance instance && instance.SelectedSlideIndex >= instance.Slides.Count)
                instance.SelectedSlideIndex = instance.Slides.Count - 1;
        }
    }
}
