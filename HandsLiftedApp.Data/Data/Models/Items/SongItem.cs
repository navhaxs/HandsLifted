using Avalonia.Media;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    //[XmlType(TypeName = "SongX")]
    [XmlRoot("Song", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItem : Item
    {

        public SongItem()
        {
            // _stanzas.CollectionChanged += _stanzas_CollectionChanged;
            // _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            // Arrangement.CollectionChanged += Arrangement_CollectionChanged;
        }

        // private void Arrangement_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        // {
        //     // debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        //
        //     if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        //     {
        //         // deselect last slide
        //         //StanzaSlides.ElementAt(e.OldStartingIndex).State
        //         //Slides.Ele
        //     }
        //
        //     //this.RaisePropertyChanged("Slides");
        // }

        public BaseSlideTheme SlideTheme { get; set; }

        // public void ResetArrangement()
        // {
        //     var a = new ObservableCollection<SongItem.Ref<SongStanza>>();
        //     foreach (var stanza in Stanzas)
        //     {
        //         a.Add(new SongItem.Ref<SongStanza>() { Value = stanza });
        //     }
        //     Arrangement = a;
        // }
        //
        // private readonly object stantaSlidesLock = new object();

        // public void GenerateSlides()
        // {
        //     UpdateStanzaSlides();
        // }
        //
        // void UpdateStanzaSlides()
        // {
        //     lock (stantaSlidesLock)
        //     {
        //         int i = 0;
        //
        //         // add title slide
        //         if (StartOnTitleSlide && TitleSlide != null)
        //         {
        //             //TitleSlide.Index = i;
        //
        //             if (this.StanzaSlides.ElementAtOrDefault(0) is SongTitleSlide)
        //             {
        //                 ((SongTitleSlide)this.StanzaSlides.ElementAt(0)).Title = Title;
        //                 ((SongTitleSlide)this.StanzaSlides.ElementAt(0)).Copyright = Copyright;
        //             }
        //             else
        //             {
        //                 this.StanzaSlides.Insert(i, TitleSlide);
        //             }
        //             i++;
        //         }
        //
        //         Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();
        //
        //         foreach (var a in Arrangement)
        //         {
        //             // todo match Stanzas by first match, so content is up to date. dont trust the cached copy in Arrangement
        //             var _datum = a.Value;
        //
        //             stanzaSeenCount[_datum.Uuid] = stanzaSeenCount.ContainsKey(_datum.Uuid) ? stanzaSeenCount[_datum.Uuid] + 1 : 0;
        //             // break slides by newlines
        //             string[] lines = _datum.Lyrics.Replace("\r\n", "\n").Split(new string[] { "\n\n" },
        //                        StringSplitOptions.RemoveEmptyEntries);
        //
        //             foreach (var x in lines.Select((line, index) => new { line, index }))
        //             {
        //                 var Text = x.line;
        //                 var Label = (x.index == 0) ? $"{_datum.Name}" : null;
        //
        //                 var slideId = $"{_datum.Uuid}:{stanzaSeenCount[_datum.Uuid]}:{x.index}";
        //
        //                 //var prev = this.StanzaSlides.ElementAtOrDefault(i);
        //                 //var prev = this.StanzaSlides.SingleOrDefault(s => s is (SongSlide<S>) && ((SongSlide<S>)s).Id == slideId);
        //                 var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index }).FirstOrDefault(s => (s.data) is SongSlide && ((SongSlide)s.data).Id == slideId);
        //
        //                 if (prevIndex != null)
        //                 {
        //                     // update the existing slide object of the same id
        //                     if (((SongSlide)prevIndex.data).Text != Text)
        //                     {
        //                         ((SongSlide)prevIndex.data).Text = Text;
        //                     }
        //
        //                     if (((SongSlide)prevIndex.data).Label != Label)
        //                     {
        //                         ((SongSlide)prevIndex.data).Label = Label;
        //                     }
        //
        //                     //prevIndex.data.Index = i;
        //
        //                     if (prevIndex.index != i)
        //                     {
        //                         //re-order to index i
        //                         this.StanzaSlides.Move(prevIndex.index, i);
        //                     }
        //                 }
        //                 else
        //                 {
        //                     var slide = new SongSlide(_datum, slideId) { Text = Text, Label = Label, }; //, Index = i };
        //                     this.StanzaSlides.Insert(i, slide);
        //                 }
        //
        //                 i++;
        //             }
        //         }
        //
        //         if (EndOnBlankSlide == true)
        //         {
        //             var prevIndex = this.StanzaSlides.Select((data, index) => new { data, index }).FirstOrDefault(s => (s.data) is (SongSlide) && ((SongSlide)s.data).Id == "BLANK");
        //             if (prevIndex != null && prevIndex.index != i)
        //             {
        //                 //prevIndex.data.Index = i;
        //                 this.StanzaSlides.Move(prevIndex.index, i);
        //             }
        //             else
        //             {
        //                 this.StanzaSlides.Insert(i, new SongSlide(null, "BLANK") { });// { Index = i });
        //             }
        //             i++;
        //         }
        //
        //         // need to delete old items
        //         while (i < this.StanzaSlides.Count)
        //         {
        //             //this.StanzaSlides.RemoveAt(i); -- BUG: wont remove unless index remove from last backwards. think about it!
        //             this.StanzaSlides.RemoveAt(this.StanzaSlides.Count - 1); // remove last
        //             i++;
        //         }
        //     }
        //     this.RaisePropertyChanged("Slides");
        // }
        //
        private Guid _design = Guid.Empty;
        public Guid Design { get => _design; set => this.RaiseAndSetIfChanged(ref _design, value); }

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
                // debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                // _stanzas.CollectionChanged -= _stanzas_CollectionChanged;
                // _stanzas.CollectionChanged += _stanzas_CollectionChanged;
                // _stanzas.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                // _stanzas.CollectionItemChanged += _stanzas_CollectionItemChanged;
            }
        }
        //
        // private void _stanzas_CollectionItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        // {
        //     s();
        // }
        //
        // private void _stanzas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        // {
        //     s();
        // }
        //
        // void s()
        // {
        //     // debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        // }

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
                // _arrangement.CollectionChanged -= Arrangement_CollectionChanged;
                // _arrangement.CollectionChanged += Arrangement_CollectionChanged;

                // debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                this.RaisePropertyChanged("Slides");
            }
        }

        //[XmlType(TypeName = "Ref")]
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

        [XmlIgnore]
        private Boolean _startOnTitleSlide = true;

        public Boolean StartOnTitleSlide { get => _startOnTitleSlide; set => this.RaiseAndSetIfChanged(ref _startOnTitleSlide, value); }

        // Stanzas + Arrangement = _stanzaSlides
        [XmlIgnore]
        private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();

        [XmlIgnore]
        public TrulyObservableCollection<Slide> StanzaSlides { get => _stanzaSlides; set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value); }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        [XmlIgnore]
        public override TrulyObservableCollection<Slide> Slides { get => _stanzaSlides; }

        // public void ReplaceWith(SongItem item)
        // {
        //     this.Title = item.Title;
        //     this.Stanzas = item.Stanzas;
        //     this.Copyright = item.Copyright;
        //     this.ResetArrangement();
        // }
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

        private string colour;
        public string Colour
        {
            get
            {
                if (colour != null)
                {
                    return colour;
                }
                else if (Name.ToLower().StartsWith("intro"))
                {
                    return "#d5c317";
                }
                else if (Name.ToLower().StartsWith("chorus"))
                {
                    return maybeStepDown(Color.Parse("#ded9fa")).ToString();
                }
                else if (Name.ToLower().StartsWith("verse"))
                {
                    return maybeStepDown(Color.Parse("#d9ecff")).ToString();
                }
                else if (Name.ToLower().StartsWith("bridge"))
                {
                    return maybeStepDown(Color.Parse("#F7D7E3")).ToString();
                }
                else
                {
                    return "#9a93cd";
                }

            }
            set => this.RaiseAndSetIfChanged(ref colour, value);
        }

        private Color maybeStepDown(Color c)
        {
            Regex regex = new Regex(@"(\d+)$",
                        RegexOptions.Compiled |
                        RegexOptions.CultureInvariant);

            Match match = regex.Match(Name);
            if (match.Success)
            {
                int verseNumber = Int32.Parse(match.Groups.Values.Last().Value);
                for (int i = 1; i < verseNumber; i++)
                {
                    c = c.Darken(0.04f);
                }

            }
            return c;
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