using DebounceThrottle;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Xml.Serialization;
using DynamicData;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class SongItemInstance : SongItem, IItemInstance
    {
        public PlaylistInstance? ParentPlaylist { get; set; }
        private SongTitleSlide titleSlide;
        private DebounceDispatcher debounceDispatcher = new(200);

        public void GenerateArrangementViews()
        {
            var result = new ObservableCollection<ArrangementRef>();
            var i = 0;
            foreach (var stanzaId in Arrangement)
            {
                var stanza = Stanzas.FirstOrDefault(stanza => stanza.Id == stanzaId);
                if (stanza != null)
                {
                    result.Add(new ArrangementRef()
                        { Index = i, SongStanza = stanza });
                    i++;
                }
            }

            ArrangementAsRefList = result;
        }

        public SongItemInstance(PlaylistInstance? parentPlaylist) : base()
        {
            ParentPlaylist = parentPlaylist;
            titleSlide = new SongTitleSlideInstance(this);

            _titleSlide = this.WhenAnyValue(x => x.Title, x => x.Copyright,
                        (title, copyright) =>
                        {
                            // TODO do not keep re-creating the slide object, rather just update it
                            titleSlide.Title = title;
                            titleSlide.Copyright = copyright;
                            return titleSlide;
                        })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Throttle(TimeSpan.FromMilliseconds(100), RxApp.TaskpoolScheduler)
                    .ToProperty(this, c => c.TitleSlide)
                ;

            // TODO: reorder...
            this.WhenAnyValue(x => x.Arrangement)
                .Subscribe(a =>
                {
                    a.CollectionChanged -= AOnCollectionChanged;
                    GenerateArrangementViews();
                    a.CollectionChanged += AOnCollectionChanged;
                });
            // TODO: reorder...
            this.WhenAnyValue(x => x.Stanzas)
                .Subscribe(a =>
                {
                    a.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                    a.CollectionChanged -= AOnCollectionChanged;
                    GenerateArrangementViews();
                    a.CollectionChanged += AOnCollectionChanged;
                    a.CollectionItemChanged += _stanzas_CollectionItemChanged;
                });
            GenerateArrangementViews();

            this.WhenAnyValue(x => x.TitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.EndOnBlankSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.StartOnTitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => { return slides.ElementAtOrDefault(selectedSlideIndex); })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            Arrangement.CollectionChanged += Arrangement_CollectionChanged;
        }

        private void AOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            GenerateArrangementViews();
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        }

        private void Arrangement_CollectionChanged(object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());

            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                // deselect last slide
                //StanzaSlides.ElementAt(e.OldStartingIndex).State
                //Slides.Ele
            }

            //this.RaisePropertyChanged("Slides");
        }

        public void ResetArrangement()
        {
            var a = new ObservableCollection<Guid>();
            foreach (var stanza in Stanzas)
            {
                a.Add(stanza.Id);
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
                try
                {
                    var newSlides = new TrulyObservableCollection<Slide>();
                    foreach (var existingSlide in Slides)
                    {
                        newSlides.Add(existingSlide);
                    }
                    
                    int i = 0;

                    // add title slide
                    if (StartOnTitleSlide && TitleSlide != null)
                    {
                        //TitleSlide.Index = i;

                        if (newSlides.ElementAtOrDefault(0) is SongTitleSlide)
                        {
                            ((SongTitleSlide)newSlides.ElementAt(0)).Title = Title;
                            ((SongTitleSlide)newSlides.ElementAt(0)).Copyright = Copyright;
                        }
                        else
                        {
                            newSlides.Insert(i, TitleSlide);
                        }

                        i++;
                    }

                    Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();

                    foreach (var a in Arrangement)
                    {
                        // todo match Stanzas by first match, so content is up to date. dont trust the cached copy in Arrangement
                        SongStanza _datum = Stanzas.First(stanza => stanza.Id == a);
                        if (_datum == null)
                        {
                            continue;
                        }

                        stanzaSeenCount[_datum.Id] =
                            stanzaSeenCount.ContainsKey(_datum.Id) ? stanzaSeenCount[_datum.Id] + 1 : 0;

                        // break slides by newlines
                        string[] lines = _datum.Lyrics.Replace("\r\n", "\n").Split(new string[] { "\n\n" },
                            StringSplitOptions.RemoveEmptyEntries);

                        foreach (var x in lines.Select((line, index) => new { line, index }))
                        {
                            var Text = x.line;
                            var Label = (x.index == 0) ? $"{_datum.Name}" : null;

                            var slideId = $"{_datum.Id}:{stanzaSeenCount[_datum.Id]}:{x.index}";

                            var prevIndex = newSlides.Select((data, index) => new { data, index })
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
                                    newSlides.Move(prevIndex.index, i);
                                }
                            }
                            else
                            {
                                var slide = new SongSlideInstance(this, _datum, slideId)
                                    { Text = Text, Label = Label, }; //, Index = i };
                                newSlides.Insert(i, slide);
                            }

                            i++;
                        }
                    }

                    if (EndOnBlankSlide == true)
                    {
                        var prevIndex = newSlides.Select((data, index) => new { data, index })
                            .FirstOrDefault(s => (s.data) is (SongSlide) && ((SongSlide)s.data).Id == "BLANK");
                        if (prevIndex != null && prevIndex.index == i)
                        {
                        }
                        else if (prevIndex != null && prevIndex.index != i)
                        {
                            //prevIndex.data.Index = i;
                            newSlides.Move(prevIndex.index, i);
                        }
                        else
                        {
                            newSlides.Insert(i,
                                new SongSlideInstance(this, new SongStanza(), "BLANK") { }); // { Index = i });
                        }

                        i++;
                    }

                    // need to delete old items
                    while (i < newSlides.Count)
                    {
                        //this.StanzaSlides.RemoveAt(i); -- BUG: wont remove unless index remove from last backwards. think about it!
                        newSlides.RemoveAt(newSlides.Count - 1); // remove last
                        i++;
                    }
                    
                    // StanzaSlides.RemoveAt(0);
                    Log.Verbose("Generated Stanza Slides. Count={Count}", newSlides.Count);
                    StanzaSlides = newSlides;
                    // this.RaisePropertyChanged("StanzaSlides");
                    this.RaisePropertyChanged("Slides");
                }
                catch (Exception ex)
                {
                    Log.Error("SongItemInstance.GenerateSlides", ex);
                }
 

            }

        }

        private void _stanzas_CollectionItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            s();
        }

        private void _stanzas_CollectionChanged(object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            s();
        }

        void s()
        {
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
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

        public ObservableCollection<Slide> Slides
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
            get => _activeSlide.Value;
        }

        // private ObservableAsPropertyHelper<ObservableCollection<ArrangementRef>> _arrangementAsRefList;
        //
        // public ObservableCollection<ArrangementRef> ArrangementAsRefList
        // {
        //     get => _arrangementAsRefList.Value;
        // }

        private ObservableCollection<ArrangementRef> _arrangementAsRefList;

        public ObservableCollection<ArrangementRef> ArrangementAsRefList
        {
            get => _arrangementAsRefList;
            set => this.RaiseAndSetIfChanged(ref _arrangementAsRefList, value);
        }
    }

    public class ArrangementRef
    {
        public int Index { get; init; }
        public SongStanza SongStanza { get; init; }

        private sealed class IndexEqualityComparer : IEqualityComparer<ArrangementRef>
        {
            public bool Equals(ArrangementRef x, ArrangementRef y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.SongStanza.Id == y.SongStanza.Id;
            }

            public int GetHashCode(ArrangementRef obj)
            {
                return obj.Index;
            }
        }

        public static IEqualityComparer<ArrangementRef> IndexComparer { get; } = new IndexEqualityComparer();
    }
}