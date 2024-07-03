using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;


namespace HandsLiftedApp.Data.Models.Items
{
    /**
     * Represents a null item
     */
    [XmlRoot("Blank", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class BlankItem : Item
    {
        public BlankItem()
        {
            Title = "(Blank)";
        }
    }
}
