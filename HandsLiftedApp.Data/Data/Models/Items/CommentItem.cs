using Avalonia.Media;
using ReactiveUI;
using System;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Types;


namespace HandsLiftedApp.Data.Models.Items
{
    /**
     * A section heading item 
     */
    [XmlRoot("SectionHeading", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class CommentItem : Item
    {
        public CommentItem()
        {
            Title = "My new comment";
        }

        public XmlColor GroupColour = Color.Parse("#4d347f");
        public Color ItemGroupAvaloniaColor { get => GroupColour; set => this.RaiseAndSetIfChanged(ref GroupColour, value); }
    }
}