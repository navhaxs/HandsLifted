namespace HandsLiftedApp.Models.UI
{
    public class NavigateToItemMessage
    {
        public int Index { get; set; }

        /// <summary>
        /// When true, the target item's header briefly flashes after navigating to it
        /// (used when the user explicitly picks an item, e.g. clicking it in the outline).
        /// </summary>
        public bool Flash { get; set; }
    }
}
