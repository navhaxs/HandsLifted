using HandsLiftedApp.Data.Slides;
using System.Collections.ObjectModel;
using System.Xml.Serialization;


namespace HandsLiftedApp.Data.Models.Items
{
    /**
     * A section heading item 
     */
    [XmlRoot("SectionHeading", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SectionHeadingItem<I> : Item<I> where I : IItemState
    {
        public SectionHeadingItem()
        {
            Title = "(New Section)";
        }

        // A section heading item itself has no slides (it is empty)
        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { };
    }
}