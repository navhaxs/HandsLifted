using Avalonia.Media;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Types;


namespace HandsLiftedApp.Data.Models.Items
{
    /**
     * A section heading item 
     */
    [XmlRoot("SectionHeading", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SectionHeadingItem : Item
    {
        public SectionHeadingItem()
        {
            Title = "";
        }

        public XmlColor GroupColour = Color.Parse("#4d347f");

        // A derived view of GroupColour for bindings - must NOT be XML-serialized. Serialized, it is written
        // as an empty <ItemGroupAvaloniaColor /> and on read is assigned default(Color), which the
        // Color -> XmlColor conversion turns into null: that wiped GroupColour on every load / Clone().
        [XmlIgnore]
        public Color ItemGroupAvaloniaColor { get => GroupColour; set => this.RaiseAndSetIfChanged(ref GroupColour, value); }
    }
}