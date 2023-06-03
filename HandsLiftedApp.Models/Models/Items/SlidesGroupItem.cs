using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    //[XmlType(TypeName = "SlidesGroupItemX")]
    [XmlRoot("SlidesGroupItem", Namespace = Constants.Namespace, IsNullable = false)]

    public class SlidesGroupItem<I, J> : Item<I> where I : IItemState where J : IItemAutoAdvanceTimerState
    {
        private J _timerState;
        [XmlIgnore]
        public J TimerState { get => _timerState; set => this.RaiseAndSetIfChanged(ref _timerState, value); }

        public SlidesGroupItem()
        {
            TimerState = (J)Activator.CreateInstance(typeof(J), this);

            //_slides = this.WhenAnyValue(x => x.Items,
            //(ObservableCollection<string> items) => generateObservableCollectionFromList(items.Select((item, index) => generateSlideFromItem(item, index))))
            //.ToProperty(this, c => c.Slides);



            // Observe any changes in the observable collection.
            // Note that the property has no public setters, so we 
            // assume the collection is mutated by using the Add(), 
            // Delete(), Clear() and other similar methods.
            this.Items.CollectionChanged += Items_CollectionChanged;
                // Convert the collection to a stream of chunks,
                // so we have IObservable<IChangeSet<TKey, TValue>>
                // type also known as the DynamicData monad.
                //.ToObservableChangeSet(x => x)
                //// Each time the collection changes, we get
                //// all updated items at once.
                //.ToCollection()
                //// If the collection isn't empty, we access the
                //// first element and check if it is an empty string.
                ////.Select(items =>

                ////    items.Any() &&
                ////    !string.IsNullOrWhiteSpace(items.First()))
                //.Select(items => items.Select((item, index) => generateSlideFromItem(item, index)))
                //.ToProperty(this, c => c._slides);
                // Then, we convert the boolean value to the
                // property. When the first string in the
                // collection isn't empty, Easy will be set
                // to True, otherwise to False.
                //.ToPropertyEx(this, x => x.Slides);
            //_Slides.CollectionChanged += (s, x) =>
            //{
            //    this.RaisePropertyChanged(nameof(Slides));
            //};
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _slides = new ObservableCollection<Slide>(Items.Select((item, index) => generateSlideFromItem(item, index)));
            this.RaisePropertyChanged("Items");
            this.RaisePropertyChanged("Slides");
        }

        private Slide generateSlideFromItem(string filename, int index) { return State.GenerateSlideFromSource(filename, index); }
        private ObservableCollection<Slide> generateObservableCollectionFromList(IEnumerable<Slide> slides) => new ObservableCollection<Slide>(slides);

        // this should be a data type for the XML
        // that is the list of slide media items <by type... video song custom etc>
        public ObservableCollection<string> _items = new ObservableCollection<string>();
        public ObservableCollection<string> Items { get => _items; set => this.RaiseAndSetIfChanged(ref _items, value); }

        private ObservableCollection<Slide> _slides = new ObservableCollection<Slide>();

        //[XmlIgnore] // TODO
        //public ObservableCollection<Slide> _slides = new ObservableCollection<Slide>();
        //{
        //    get => _internal_slides; set
        //    {
        //        this.RaiseAndSetIfChanged(ref _internal_slides, value);
        //        this.RaisePropertyChanged(nameof(Slides));
        //        _internal_slides.CollectionChanged += (s, x) =>
        //        {
        //            this.RaisePropertyChanged(nameof(_Slides));
        //            this.RaisePropertyChanged(nameof(Slides));
        //        };
        //    }
        //}
        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        [XmlIgnore]
        public override ObservableCollection<Slide> Slides { get => _slides; }
        //get => _slides.Value; }

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
        public SlidesGroupItem<I, J>? slice(int start)
        {
            if (start == 0)
            {
                return null;
            }

            SlidesGroupItem<I, J> slidesGroup = new SlidesGroupItem<I, J>() { Title = $"{Title} (Split copy)" };

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
