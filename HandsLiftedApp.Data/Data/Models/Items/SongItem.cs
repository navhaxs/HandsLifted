using Avalonia.Media;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using DynamicData;
using DynamicData.Binding;

namespace HandsLiftedApp.Data.Models.Items
{
    //[XmlType(TypeName = "SongX")]
    [XmlRoot("Song", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItem : Item
    {
        public SongItem()
        {
            


        
        }

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
            }
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

        private string? _selectedArrangementId;
        public string? SelectedArrangementId { get => _selectedArrangementId; set => this.RaiseAndSetIfChanged(ref _selectedArrangementId, value); }

        [XmlIgnore]
        private ObservableCollection<Guid> _arrangement = new ObservableCollection<Guid>();

        public ObservableCollection<Guid> Arrangement
        {
            get => _arrangement;
            set => this.RaiseAndSetIfChanged(ref _arrangement, value);
        }

        [XmlIgnore]
        private Boolean _endOnBlankSlide = true;

        public Boolean EndOnBlankSlide { get => _endOnBlankSlide; set => this.RaiseAndSetIfChanged(ref _endOnBlankSlide, value); }

        [XmlIgnore]
        private Boolean _startOnTitleSlide = true;

        public Boolean StartOnTitleSlide { get => _startOnTitleSlide; set => this.RaiseAndSetIfChanged(ref _startOnTitleSlide, value); }

        // Stanzas + Arrangement = _stanzaSlides
        // [XmlIgnore]
        // private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();
        //
        // [XmlIgnore]
        // public TrulyObservableCollection<Slide> StanzaSlides { get => _stanzaSlides; set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value); }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        // [XmlIgnore]
        // public override TrulyObservableCollection<Slide> Slides { get => _stanzaSlides; }

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
        public Guid Id { get; set; } = Guid.NewGuid();

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

        public SongStanza(Guid id, string Name, string Lyrics)
        {
            this.Id = id;
            this.Name = Name;
            this.Lyrics = Lyrics;
        }
    }
}