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

    }
}
