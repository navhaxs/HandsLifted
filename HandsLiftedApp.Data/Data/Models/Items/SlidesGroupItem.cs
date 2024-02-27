using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    //[XmlType(TypeName = "SlidesGroupItemX")]
    [XmlRoot("SlidesGroupItem", Namespace = Constants.Namespace, IsNullable = false)]
    //
    // TODO: need to define list of media, rather than Slide ??? for serialization
    [XmlInclude(typeof(ImageSlide))]
    [XmlInclude(typeof(VideoSlide))]
    [Serializable]
    public class SlidesGroupItem : Item
    {

        public SlidesGroupItem()
        {
            _items.CollectionChanged += _items_CollectionChanged;
        }

        // this should be a data type for the XML
        // that is the list of slide media items <by type... video song custom etc>
        [XmlIgnore]
        public TrulyObservableCollection<MediaSlide> _items = new TrulyObservableCollection<MediaSlide>();
        public TrulyObservableCollection<MediaSlide> Items
        {
            get => _items; set
            {
                this.RaiseAndSetIfChanged(ref _items, value);
                _items.CollectionChanged += _items_CollectionChanged;
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        private void _items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(Items));
            this.RaisePropertyChanged(nameof(Slides));
        }

        private bool _IsLooping = false;

        /// <summary>
        /// Loop back to the first slide of the item once reaching the end 
        /// </summary>
        public bool IsLooping { get => _IsLooping; set => this.RaiseAndSetIfChanged(ref _IsLooping, value); }

        private ItemAutoAdvanceTimer _AutoAdvanceTimer = new ItemAutoAdvanceTimer();
        public ItemAutoAdvanceTimer AutoAdvanceTimer { get => _AutoAdvanceTimer; set => this.RaiseAndSetIfChanged(ref _AutoAdvanceTimer, value); }

        /// <summary>
        /// mutates *this* SlidesGroupItem and then returns a *new* SlidesGroupItem
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public SlidesGroupItem? slice(int start)
        {
            if (start == 0)
            {
                return null;
            }

            SlidesGroupItem slidesGroup = new SlidesGroupItem{ Title = $"{Title} (Split copy)" };

            // TODO optimise below to a single loop
            // tricky bit: ensure index logic works whilst removing at the same time

            for (int i = start; i < Items.Count; i++)
            {
                slidesGroup.Items.Add(Items[i]);
            }

            var count = Items.Count;
            for (int i = start; i < count; i++)
            {
                Items.RemoveAt(Items.Count - 1);
            }

            return slidesGroup;
        }
    }
}
