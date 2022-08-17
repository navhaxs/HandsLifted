using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Song", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItem<T, S, I> : Item<I> where T : ISongTitleSlideState where S : ISongSlideState where I : IItemState
    {

        public SongItem()
        {
            _titleSlide = this.WhenAnyValue(x => x.Title, x => x.Copyright,
                (title, copyright) =>
                {
                    return new SongTitleSlide<T>() { Title = Title, Copyright = Copyright };
                    ;
                })
                .ToProperty(this, c => c.TitleSlide)
            ;

            _stanzas.CollectionChanged += _stanzas_CollectionChanged;
            Arrangement.CollectionChanged += Arrangement_CollectionChanged; ;

            // TODO 
            //_stanzaSlides = this.Stanzas
            //    // Convert the collection to a stream of chunks,
            //    // so we have IObservable<IChangeSet<TKey, TValue>>
            //    // type also known as the DynamicData monad.
            //    .ToObservableChangeSet(x => x)
            //    // Each time the collection changes, we get
            //    // all updated items at once.
            //    .ToCollection()
            //    .Select((collection) => processSongStanzasToSlides(collection))
            //    .ToProperty(this, c => c.StanzaSlides)
            //;

            //_slides = this.StanzaSlides.ToObservableChangeSet().ToCollection()
            ////_slides = this.WhenAnyValue(x => x.TitleSlide, x => x.StanzaSlides,
            ////    (titleSlide, stanzaSlides) =>
            ////    {
            ////        var x = new ObservableCollection<Slide>();

            ////        if (titleSlide != null)
            ////            x.Add(titleSlide);

            ////        if (stanzaSlides != null)
            ////            x.AddRange(stanzaSlides);

            ////        // TODO: add bool value
            ////        x.Add(new SongSlide<S>(null));

            ////        return x;
            ////    })
            //    .ToProperty(this, c => c.Slides)
            //;
        }

        private void Arrangement_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateStanzaSlides();
        }

        void UpdateStanzaSlides()
        {
            int i = 0;

            // TODO add title slide

            Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();

            foreach (var a in Arrangement)
            //foreach (SongStanza _datum in this.Stanzas)
            {
                var _datum = a.Value;

                stanzaSeenCount[_datum.Uuid] = stanzaSeenCount.ContainsKey(_datum.Uuid) ? stanzaSeenCount[_datum.Uuid] + 1 : 0;
                // break slides by newlines
                string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
                           StringSplitOptions.RemoveEmptyEntries);

                foreach (var x in lines.Select((line, index) => new { line, index }))
                {
                    var Text = x.line;
                    var Label = (x.index == 0) ? $"{_datum.Name}" : null;

                    var slideId = $"{_datum.Uuid}:{stanzaSeenCount[_datum.Uuid]}:{x.index}";

                    //var prev = this.StanzaSlides.ElementAtOrDefault(i);
                    //var prev = this.StanzaSlides.SingleOrDefault(s => s is (SongSlide<S>) && ((SongSlide<S>)s).Id == slideId);
                    var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index }).FirstOrDefault(s => (s.data) is (SongSlide<S>) && ((SongSlide<S>)s.data).Id == slideId);

                    if (prevIndex != null)
                    {
                        // update the existing slide object of the same id
                        if (((SongSlide<S>)prevIndex.data).Text != Text)
                        {
                            ((SongSlide<S>)prevIndex.data).Text = Text;
                        }

                        if (((SongSlide<S>)prevIndex.data).Label != Label)
                        {
                            ((SongSlide<S>)prevIndex.data).Label = Label;
                        }

                        if (prevIndex.index != i)
                        {
                            //re-order to index i
                            this.StanzaSlides.Move(prevIndex.index, i);
                        }
                    }
                    else
                    {
                        var slide = new SongSlide<S>(_datum, slideId) { Text = Text, Label = Label };
                        this.StanzaSlides.Insert(i, slide);
                    }


                    i++;
                }
            }

            // TODO: add blank slide

            // need to delete old items
            while (i < this.StanzaSlides.Count)
            {
                this.StanzaSlides.RemoveAt(i);
                i++;
            }
        }

        private string _copyright = "";
        public string Copyright { get => _copyright; set => this.RaiseAndSetIfChanged(ref _copyright, value); }

        private TrulyObservableCollection<SongStanza> _stanzas = new TrulyObservableCollection<SongStanza>();
        public TrulyObservableCollection<SongStanza> Stanzas
        {
            get => _stanzas;
            set
            {
                this.RaiseAndSetIfChanged(ref _stanzas, value);
                _stanzas.CollectionChanged -= _stanzas_CollectionChanged;
                _stanzas.CollectionChanged += _stanzas_CollectionChanged;
            }
        }

        private void _stanzas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateStanzaSlides();

            //if (Arrangement.Count == 0)
            //{
            Arrangement.Clear();
            foreach (var stanza in Stanzas)
            {
                Arrangement.Add(new SongItem<T, S, I>.Ref<SongStanza>() { Value = stanza });
            }
            //}
        }

        /*
         * Arrangements
         * 
         * a dictionary of arrangements by "Arrangement Id"
         * 
         * e.g.
         * "Standard" : ["Verse 1", "Chorus", "Verse 2", "Chorus", "Verse 3", "Chorus", "Verse 4", "Chorus", "Chorus"]
         * "Special" : ["Verse 1", "Chorus", "Verse 2", "Chorus", "Verse 3", "Chorus", "Chorus"]
         */
        private Dictionary<string, List<Guid>> _arrangements = new Dictionary<string, List<Guid>>();
        public Dictionary<string, List<Guid>> Arrangements { get => _arrangements; set => this.RaiseAndSetIfChanged(ref _arrangements, value); }

        private string _selectedArrangementId;
        public string SelectedArrangementId { get => _selectedArrangementId; set => this.RaiseAndSetIfChanged(ref _selectedArrangementId, value); }

        private ObservableCollection<Ref<SongStanza>> _arrangement = new ObservableCollection<Ref<SongStanza>>();

        public ObservableCollection<Ref<SongStanza>> Arrangement { get => _arrangement; set => this.RaiseAndSetIfChanged(ref _arrangement, value); }

        public class Ref<X>
        {
            public X Value { get; set; }
        }


        //private ObservableAsPropertyHelper<Slide> _titleSlide;
        //[XmlIgnore]

        //public Slide TitleSlide { get => _titleSlide.Value; }

        private ObservableAsPropertyHelper<Slide> _titleSlide;
        [XmlIgnore]

        public Slide TitleSlide { get => _titleSlide.Value; }

        // Stanzas + Arrangement = _stanzaSlides
        private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();
        [XmlIgnore]

        public TrulyObservableCollection<Slide> StanzaSlides { get => _stanzaSlides; set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value); }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        [XmlIgnore]

        public override ObservableCollection<Slide> Slides { get => _stanzaSlides; }

        //public List<Slide> processSongStanzasToSlides(IReadOnlyCollection<SongStanza> stanzas)
        //{
        //    List<Slide> slides = new List<Slide>();

        //    foreach (SongStanza _datum in stanzas)
        //    {
        //        // break slides by newlines
        //        string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
        //                   StringSplitOptions.RemoveEmptyEntries);
        //        foreach (var x in lines.Select((line, index) => new { line, index }))
        //        //foreach (string line in lines)
        //        {
        //            var slide = new SongSlide<S>(_datum) { Text = x.line };

        //            if (x.index == 0)
        //            {
        //                slide.Label = $"{_datum.Name}";
        //            }

        //            slides.Add(slide);
        //        }
        //    }

        //    return slides;
        //}

        private void Stanzas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

            //List<Slide> slides = new List<Slide>();

            //foreach (var _datum in Stanzas)
            //{
            //    // break slides by newlines
            //    string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
            //               StringSplitOptions.RemoveEmptyEntries);
            //    foreach (string line in lines)
            //    {
            //        slides.Add(new SongSlide() { Text = line });
            //    }
            //}

            //Slides = slides;
            //this.RaisePropertyChanged(nameof(Slides));
            //Slides.Clear();
            //Slides.AddRange(processSongStanzasToSlides(this.Stanzas));


            //switch (e.Action)
            //{
            //    case NotifyCollectionChangedAction.Add:
            //        foreach (var item in e.NewItems)
            //        {
            //            //    // break slides by newlines
            //            //    string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
            //            //               StringSplitOptions.RemoveEmptyEntries);
            //            //    foreach (string line in lines)
            //            //    {
            //            //        slides.Add(new SongSlide() { Text = line });
            //            //    }

            //            if (item is SongStanza)
            //            {
            //                string[] lines = ((SongStanza)item).Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
            //                               StringSplitOptions.RemoveEmptyEntries);
            //                foreach (string line in lines)
            //                {
            //                    Slides.Add(new SongSlide((SongStanza)item) { Text = line });
            //                }
            //            }
            //        }
            //        //Slides.AddRange(processSongStanzasToSlides(e.NewItems));
            //        break;
            //    case NotifyCollectionChangedAction.Move:
            //        // implement...
            //        break;
            //    case NotifyCollectionChangedAction.Remove:
            //        //this.RemoveItems(e.OldItems.OfType<T>());
            //        break;
            //    case NotifyCollectionChangedAction.Replace:
            //        // implement...
            //        foreach (var item in e.NewItems)
            //        {
            //            if (item is SongStanza)
            //            {
            //                string[] lines = ((SongStanza)item).Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
            //                               StringSplitOptions.RemoveEmptyEntries);
            //                foreach (string line in lines)
            //                {
            //                    Slides.Insert(e.NewStartingIndex, new SongSlide((SongStanza)item) { Text = line });
            //                }
            //            }
            //        }
            //        break;
            //    case NotifyCollectionChangedAction.Reset:
            //        //this.ReplaceAll(_synchronizedCollection.Items);
            //        break;
            //}
        }
    }

    public class SongStanza : ReactiveObject
    {
        public Guid Uuid { get; set; }

        private string name;
        public string Name
        {
            get => name;
            set => this.RaiseAndSetIfChanged(ref name, value);
        }

        private string lyrics;
        public string Lyrics
        {
            get => lyrics;
            set => this.RaiseAndSetIfChanged(ref lyrics, value);
        }

        // parameter-less constructor required for serialization
        public SongStanza()
        {
        }

        public SongStanza(Guid Uuid, string Name, string Lyrics)
        {
            this.Uuid = Uuid;
            this.Name = Name;
            this.Lyrics = Lyrics;
        }
    }
}