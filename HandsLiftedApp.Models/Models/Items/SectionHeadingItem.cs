using Avalonia.Media;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private Color _itemGroupColour = Color.Parse("#3a344a");
        public Color ItemGroupColour { get => _itemGroupColour; set => this.RaiseAndSetIfChanged(ref _itemGroupColour, value); }

        // A section heading item itself has no slides (it is empty)
        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { };
    }
}