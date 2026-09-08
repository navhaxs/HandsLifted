using System;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    /// <summary>
    /// A playlist item that references a library SongItem by UUID rather than embedding
    /// its content. Only UUID (inherited from Item, persisted since Item.UUID stopped being
    /// [XmlIgnore]) carries meaning — resolution happens against SongLibraryIndex at runtime.
    /// </summary>
    [XmlRoot("SongReference", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItemReference : Item
    {
        public override Item Clone()
        {
            // A reference's UUID identifies which library song it points at, not this
            // playlist item's own identity. The base Clone() reassigns a fresh UUID for
            // independent duplicate-item identity, which is correct for content-owning
            // item types but would orphan a reference from the song it points at.
            return new SongItemReference { UUID = UUID, SlideTransitionDurationMs = SlideTransitionDurationMs };
        }
    }
}
