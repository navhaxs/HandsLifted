using System;
using System.Collections.Generic;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    /// <summary>
    /// An imported item whose slide list is replaced by a Sync. While a slide is held, that exact slide stays the
    /// item's active (live) slide, whatever the new slide list looks like; the next navigation into the item
    /// (a click, Next, Prev) releases it.
    /// </summary>
    public interface ISlideHoldable
    {
        Slide? HeldSlide { get; set; }
    }

    internal static class SyncSlideHold
    {
        /// <summary>
        /// Call just before a Sync replaces the item's slides. If one of this item's slides is the live slide, it
        /// is held so the audience is not interrupted by a slide that may no longer match the old position.
        /// </summary>
        public static void Begin(IItemInstance item, PlaylistInstance? playlist)
        {
            if (item is not ISlideHoldable holdable || playlist == null) return;
            if (!ReferenceEquals(playlist.SelectedItem, item) || item.SelectedSlideIndex < 0) return;

            var active = item.ActiveSlide;
            if (active == null || active is BlankSlide) return;

            holdable.HeldSlide = active;
        }

        /// <summary>
        /// Call after a Sync has replaced the item's slides. If a slide was held, the item's selected index moves to
        /// where that same slide is now (see <see cref="SlideIdentity"/>) so highlight, Next and Prev carry on from the
        /// right place; the held slide itself stays live. If the slide can't be found the old position is kept (clamped).
        /// </summary>
        public static void Follow(IItemInstance item, IReadOnlyList<string?>? oldKeys, int oldIndex,
            IReadOnlyList<string?> newKeys)
        {
            if (item is not ISlideHoldable { HeldSlide: not null }) return;
            if (newKeys.Count == 0) return;

            var target = SlideIdentity.MapIndex(oldKeys, oldIndex, newKeys);
            if (target < 0) target = Math.Min(oldIndex, newKeys.Count - 1);
            if (target >= 0) item.SelectedSlideIndex = target;
        }
    }
}
