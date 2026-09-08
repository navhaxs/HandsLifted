using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    /// <summary>
    /// A playlist item that references a library SongItem by SongId rather than embedding
    /// its content. Item.UUID (inherited, persisted) remains this playlist item's own
    /// identity — used by slide navigation (PlaylistInstance.NavigateToReference) and
    /// drag-reorder (SlideThumbnailBehavior), which both key off it and would silently
    /// resolve to the wrong item if two references shared a UUID. SongId is the separate,
    /// independent key that resolves against SongLibraryIndex at runtime — deliberately
    /// distinct from UUID so duplicating a reference (Clone) can preserve which song it
    /// points at while still getting its own fresh playlist-item identity, matching every
    /// other item type's Clone() behavior.
    /// </summary>
    [XmlRoot("SongReference", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItemReference : Item
    {
        public Guid SongId { get; set; }
    }
}
