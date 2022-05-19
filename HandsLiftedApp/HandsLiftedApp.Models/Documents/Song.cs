using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Documents
{
    [XmlRoot("Song", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class Song : SlidesDocument
    {
        public Guid Uuid { get; set; }

        public string Title { get; set; }

        public string Copyright { get; set; }

        private TrulyObservableCollection<SongStanza> stanzas = new TrulyObservableCollection<SongStanza>();
        public TrulyObservableCollection<SongStanza> Stanzas
        {
            get => stanzas;
            set => this.RaiseAndSetIfChanged(ref stanzas, value);
        }
        //public List<KeyValuePair<string, List<Guid>>> Arrangements { get; set; } = new List<KeyValuePair<string, List<Guid>>>();


        //internal slides

        private ObservableAsPropertyHelper<IEnumerable<Slide>> _slides;
        public override IEnumerable<Slide> Slides => _slides.Value;

        public Song()
        {
            Stanzas.CollectionChanged += Stanzas_CollectionChanged;
            _slides = this.Stanzas
                 .ToObservableChangeSet(x => x)
        // Each time the collection changes, we get
        // all updated items at once.
        .ToCollection()
              .Select(_data =>
              {
                  List<Slide> slides = new List<Slide>();

                  foreach (var _datum in _data)
                  {
                      // break slides by newlines
                      string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
                                 StringSplitOptions.RemoveEmptyEntries);
                      foreach (string line in lines)
                      {
                          slides.Add(new SongSlide() { Text = line });
                      }
                  }

                  return slides;
              })
              .ToProperty(this, x => x.Slides);
        }

        private void Stanzas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.Print("MMM MMM MMM");
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
            public SongStanza(Guid Uuid, string Name, string Lyrics)
            {
                this.Uuid = Uuid;
                this.Name = Name;
                this.Lyrics = Lyrics;
            }
        }
    }
}