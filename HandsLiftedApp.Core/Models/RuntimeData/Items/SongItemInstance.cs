using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Avalonia.Media;
using DebounceThrottle;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{

    public class SongItemInstance : SongItem, IItemInstance
    {
        public PlaylistInstance? ParentPlaylist { get; set; } 
        private SongTitleSlide titleSlide;
        private DebounceDispatcher debounceDispatcher = new DebounceDispatcher(200);


        // public SongItemInstance() : this(null)
        // {
        //     
        // }
        public SongItemInstance(PlaylistInstance? parentPlaylist) : base()
        {
            ParentPlaylist = parentPlaylist;   
            titleSlide = new SongTitleSlideInstance(this) { };
            //{
            //    Title = Title,
            //    Copyright = Copyright
            //};
            _titleSlide = this.WhenAnyValue(x => x.Title, x => x.Copyright,
                        (title, copyright) =>
                        {
                            // TODO do not keep re-creating the slide object, rather just update it
                            titleSlide.Title = title;
                            titleSlide.Copyright = copyright;
                            return titleSlide;
                        })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                    .ToProperty(this, c => c.TitleSlide)
                ;

            this.WhenAnyValue(x => x.TitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.EndOnBlankSlide, x => x.StartOnTitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => { return slides.ElementAtOrDefault(selectedSlideIndex); })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            _stanzas.CollectionChanged += _stanzas_CollectionChanged;
            _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            Arrangement.CollectionChanged += Arrangement_CollectionChanged;
        }

        private void Arrangement_CollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                // deselect last slide
                //StanzaSlides.ElementAt(e.OldStartingIndex).State
                //Slides.Ele
            }

            //this.RaisePropertyChanged("Slides");
        }

        public BaseSlideTheme SlideTheme { get; set; }

        public void ResetArrangement()
        {
            var a = new ObservableCollection<SongItemInstance.Ref<SongStanza>>();
            foreach (var stanza in Stanzas)
            {
                a.Add(new SongItemInstance.Ref<SongStanza>() { Value = stanza });
            }

            Arrangement = a;
        }

        private readonly object stantaSlidesLock = new object();

        public void GenerateSlides()
        {
            UpdateStanzaSlides();
        }

        void UpdateStanzaSlides()
        {
            lock (stantaSlidesLock)
            {
                int i = 0;

                // add title slide
                if (StartOnTitleSlide && TitleSlide != null)
                {
                    //TitleSlide.Index = i;

                    if (this.StanzaSlides.ElementAtOrDefault(0) is SongTitleSlide)
                    {
                        ((SongTitleSlide)this.StanzaSlides.ElementAt(0)).Title = Title;
                        ((SongTitleSlide)this.StanzaSlides.ElementAt(0)).Copyright = Copyright;
                    }
                    else
                    {
                        this.StanzaSlides.Insert(i, TitleSlide);
                    }

                    i++;
                }

                Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();

                foreach (var a in Arrangement)
                {
                    // todo match Stanzas by first match, so content is up to date. dont trust the cached copy in Arrangement
                    SongStanza _datum = a.Value;

                    stanzaSeenCount[_datum.Uuid] =
                        stanzaSeenCount.ContainsKey(_datum.Uuid) ? stanzaSeenCount[_datum.Uuid] + 1 : 0;
                    
                    
                    // TODO
                    // TODO
                    // TODO
                    // TODO
                    // TODO
                    // TODO
                    
                    // break slides by newlines
                    string[] lines = _datum.Lyrics.Replace("\r\n", "\n").Split(new string[] { "\n\n" },
                        StringSplitOptions.RemoveEmptyEntries);

                    foreach (var x in lines.Select((line, index) => new { line, index }))
                    {
                        var Text = x.line;
                        var Label = (x.index == 0) ? $"{_datum.Name}" : null;

                        var slideId = $"{_datum.Uuid}:{stanzaSeenCount[_datum.Uuid]}:{x.index}";

                        //var prev = this.StanzaSlides.ElementAtOrDefault(i);
                        //var prev = this.StanzaSlides.SingleOrDefault(s => s is (SongSlide<S>) && ((SongSlide<S>)s).Id == slideId);
                        var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index })
                            .FirstOrDefault(s => (s.data) is SongSlide && ((SongSlide)s.data).Id == slideId);

                        if (prevIndex != null)
                        {
                            // update the existing slide object of the same id
                            if (((SongSlide)prevIndex.data).Text != Text)
                            {
                                ((SongSlide)prevIndex.data).Text = Text;
                            }

                            if (((SongSlide)prevIndex.data).Label != Label)
                            {
                                ((SongSlide)prevIndex.data).Label = Label;
                            }

                            //prevIndex.data.Index = i;

                            if (prevIndex.index != i)
                            {
                                //re-order to index i
                                this.StanzaSlides.Move(prevIndex.index, i);
                            }
                        }
                        else
                        {
                            var slide = new SongSlideInstance(this, _datum, slideId) { Text = Text, Label = Label, }; //, Index = i };
                            this.StanzaSlides.Insert(i, slide);
                        }

                        i++;
                    }
                }

                if (EndOnBlankSlide == true)
                {
                    var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index })
                        .FirstOrDefault(s => (s.data) is (SongSlide) && ((SongSlide)s.data).Id == "BLANK");
                    if (prevIndex != null && prevIndex.index != i)
                    {
                        //prevIndex.data.Index = i;
                        this.StanzaSlides.Move(prevIndex.index, i);
                    }
                    else
                    {
                        this.StanzaSlides.Insert(i, new SongSlideInstance(this, null, "BLANK") { }); // { Index = i });
                    }

                    i++;
                }

                // need to delete old items
                while (i < this.StanzaSlides.Count)
                {
                    //this.StanzaSlides.RemoveAt(i); -- BUG: wont remove unless index remove from last backwards. think about it!
                    this.StanzaSlides.RemoveAt(this.StanzaSlides.Count - 1); // remove last
                    i++;
                }
            }

            this.RaisePropertyChanged("Slides");
        }

        // private string _design = "";
        //
        // public string Design
        // {
        //     get => _design;
        //     set => this.RaiseAndSetIfChanged(ref _design, value);
        // }

        [XmlIgnore] private string _copyright = "";

        public string Copyright
        {
            get => _copyright;
            set => this.RaiseAndSetIfChanged(ref _copyright, value);
        }

        [XmlIgnore]
        private TrulyObservableCollection<SongStanza> _stanzas = new TrulyObservableCollection<SongStanza>();

        public TrulyObservableCollection<SongStanza> Stanzas
        {
            get => _stanzas;
            set
            {
                _stanzas.CollectionChanged -= _stanzas_CollectionChanged;
                _stanzas.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                this.RaiseAndSetIfChanged(ref _stanzas, value);
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                _stanzas.CollectionChanged += _stanzas_CollectionChanged;
                _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            }
        }

        private void _stanzas_CollectionItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            s();
        }

        private void _stanzas_CollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            s();
        }

        void s()
        {
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
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
        [XmlIgnore] private SerializableDictionary<string, List<Guid>> _arrangements =
            new SerializableDictionary<string, List<Guid>>();

        public SerializableDictionary<string, List<Guid>> Arrangements
        {
            get => _arrangements;
            set => this.RaiseAndSetIfChanged(ref _arrangements, value);
        }

        private string _selectedArrangementId;

        public string SelectedArrangementId
        {
            get => _selectedArrangementId;
            set => this.RaiseAndSetIfChanged(ref _selectedArrangementId, value);
        }

        [XmlIgnore] private ObservableCollection<Ref<SongStanza>> _arrangement =
            new ObservableCollection<Ref<SongStanza>>();

        public ObservableCollection<Ref<SongStanza>> Arrangement
        {
            get => _arrangement;
            set
            {
                this.RaiseAndSetIfChanged(ref _arrangement, value);
                _arrangement.CollectionChanged -= Arrangement_CollectionChanged;
                _arrangement.CollectionChanged += Arrangement_CollectionChanged;

                // debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                this.RaisePropertyChanged("Slides");
            }
        }

        [XmlIgnore] private ObservableAsPropertyHelper<Slide> _titleSlide;

        [XmlIgnore]
        public Slide TitleSlide
        {
            get => _titleSlide.Value;
        }

        [XmlIgnore] private Boolean _endOnBlankSlide = true;

        public Boolean EndOnBlankSlide
        {
            get => _endOnBlankSlide;
            set => this.RaiseAndSetIfChanged(ref _endOnBlankSlide, value);
        }

        // Stanzas + Arrangement = _stanzaSlides
        [XmlIgnore] private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();

        [XmlIgnore]
        public TrulyObservableCollection<Slide> StanzaSlides
        {
            get => _stanzaSlides;
            set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value);
        }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        [XmlIgnore]
        public override TrulyObservableCollection<Slide> Slides
        {
            get => _stanzaSlides;
        }

        public void ReplaceWith(SongItemInstance itemInstance)
        {
            this.Title = itemInstance.Title;
            this.Stanzas = itemInstance.Stanzas;
            this.Copyright = itemInstance.Copyright;
            this.ResetArrangement();
        }

        private int _selectedSlideIndex = -1;

        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide?.Value;
        }
    }
}