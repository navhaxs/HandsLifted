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
    }
}
