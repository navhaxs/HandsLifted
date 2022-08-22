using Avalonia.Controls;
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
            _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            Arrangement.CollectionChanged += Arrangement_CollectionChanged;
        }

        private void Arrangement_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateStanzaSlides();
            this.RaisePropertyChanged("Slides");
        }

        void UpdateStanzaSlides()
        {
            int i = 0;

            // TODO add title slide
            if (TitleSlide != null)
            {
                TitleSlide.Index = i;

                if (this.StanzaSlides.ElementAtOrDefault(0) is SongTitleSlide<T>)
                {
                    ((SongTitleSlide<T>)this.StanzaSlides.ElementAt(0)).Title = Title;
                    ((SongTitleSlide<T>)this.StanzaSlides.ElementAt(0)).Copyright = Copyright;
                    //((SongTitleSlide<T>)this.StanzaSlides.ElementAt(0)).State = Copyright;
                }
                else
                {
                    this.StanzaSlides.Add(TitleSlide);
                }
                i++;
            }

            Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();

            foreach (var a in Arrangement)
            {
                // todo match Stanzas by first match, so content is up to date. dont trust the cached copy in Arrangement
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

                        prevIndex.data.Index = i;

                        if (prevIndex.index != i)
                        {
                            //re-order to index i
                            this.StanzaSlides.Move(prevIndex.index, i);
                        }
                    }
                    else
                    {
                        var slide = new SongSlide<S>(_datum, slideId) { Text = Text, Label = Label, Index = i };
                        this.StanzaSlides.Insert(i, slide);
                    }

                    i++;
                }
            }

            // TODO add blank slide
            if (EndOnBlankSlide == true)
            {
                var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index }).FirstOrDefault(s => (s.data) is (SongSlide<S>) && ((SongSlide<S>)s.data).Id == "BLANK");
                if (prevIndex != null && prevIndex.index != i)
                {
                    prevIndex.data.Index = i;
                    this.StanzaSlides.Move(prevIndex.index, i);
                }
                else
                {
                    this.StanzaSlides.Insert(i, new SongSlide<S>(null, "BLANK") { Index = i });
                }
                i++;
            }

            // need to delete old items
            while (i < this.StanzaSlides.Count)
            {
                this.StanzaSlides.RemoveAt(i);
                i++;
            }
        }

        [XmlIgnore]
        private string _copyright = "";
        public string Copyright { get => _copyright; set => this.RaiseAndSetIfChanged(ref _copyright, value); }

        [XmlIgnore]
        private TrulyObservableCollection<SongStanza> _stanzas = new TrulyObservableCollection<SongStanza>();
        public TrulyObservableCollection<SongStanza> Stanzas
        {
            get => _stanzas;
            set
            {
                this.RaiseAndSetIfChanged(ref _stanzas, value);
                UpdateStanzaSlides();
                _stanzas.CollectionChanged -= _stanzas_CollectionChanged;
                _stanzas.CollectionChanged += _stanzas_CollectionChanged;
                _stanzas.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            }
        }

        private void _stanzas_CollectionItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            s();
        }

        private void _stanzas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            s();
        }

        void s()
        {
            UpdateStanzaSlides();

            var a = new ObservableCollection<Ref<SongStanza>>();
            foreach (var stanza in Stanzas)
            {
                a.Add(new SongItem<T, S, I>.Ref<SongStanza>() { Value = stanza });
            }
            Arrangement = a;
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
        [XmlIgnore]
        private SerializableDictionary<string, List<Guid>> _arrangements = new SerializableDictionary<string, List<Guid>>();
        public SerializableDictionary<string, List<Guid>> Arrangements { get => _arrangements; set => this.RaiseAndSetIfChanged(ref _arrangements, value); }

        private string _selectedArrangementId;
        public string SelectedArrangementId { get => _selectedArrangementId; set => this.RaiseAndSetIfChanged(ref _selectedArrangementId, value); }

        [XmlIgnore]
        private ObservableCollection<Ref<SongStanza>> _arrangement = new ObservableCollection<Ref<SongStanza>>();

        public ObservableCollection<Ref<SongStanza>> Arrangement
        {
            get => _arrangement; set
            {
                this.RaiseAndSetIfChanged(ref _arrangement, value);
                _arrangement.CollectionChanged -= Arrangement_CollectionChanged;
                _arrangement.CollectionChanged += Arrangement_CollectionChanged;
            }
        }

        public class Ref<X>
        {
            public X Value { get; set; }
        }

        [XmlIgnore]
        private ObservableAsPropertyHelper<Slide> _titleSlide;
        
        [XmlIgnore]
        public Slide TitleSlide { get => _titleSlide.Value; }

        [XmlIgnore]
        private Boolean _endOnBlankSlide = true;

        public Boolean EndOnBlankSlide { get => _endOnBlankSlide; set => this.RaiseAndSetIfChanged(ref _endOnBlankSlide, value); }

        // Stanzas + Arrangement = _stanzaSlides
        [XmlIgnore]
        private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();
        
        [XmlIgnore]
        public TrulyObservableCollection<Slide> StanzaSlides { get => _stanzaSlides; set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value); }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        [XmlIgnore]
        public override TrulyObservableCollection<Slide> Slides { get => _stanzaSlides; }
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