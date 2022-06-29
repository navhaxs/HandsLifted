﻿using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Song", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SongItem : Item
    {
        public Guid Uuid { get; set; }

        public string _title;
        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        public string _copyright;
        public string Copyright
        {
            get => _copyright; set

            {
                this.RaiseAndSetIfChanged(ref _copyright, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        private BindingList<SongStanza> _stanzas = new BindingList<SongStanza>();
        public BindingList<SongStanza> Stanzas
        {
            get => _stanzas;
            set
            {
                this.RaiseAndSetIfChanged(ref _stanzas, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }
        //public List<KeyValuePair<string, List<Guid>>> Arrangements { get; set; } = new List<KeyValuePair<string, List<Guid>>>();


        private ObservableAsPropertyHelper<Slide> _titleSlide;
        public Slide TitleSlide { get => _titleSlide.Value; }


        private ObservableAsPropertyHelper<List<Slide>> _stanzaSlides;

        public List<Slide> StanzaSlides { get => _stanzaSlides.Value; }

        private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        public override ObservableCollection<Slide> Slides { get => _slides.Value; }

        public SongItem()
        {
            //Stanzas.CollectionChanged += Stanzas_CollectionChanged;

            //StanzaSlides.

            _titleSlide = this.WhenAnyValue(x => x.Title, x => x.Copyright,
                (title, copyright) =>
                {
                    return new SongTitleSlide() { Title = Title, Copyright = Copyright };
                    ;
                })
                .ToProperty(this, c => c.TitleSlide)
            ;

            _stanzaSlides = this.WhenAny(x => x.Stanzas,
                (stanzas) =>
                {
                    return processSongStanzasToSlides(stanzas.Value);
                })
                .ToProperty(this, c => c.StanzaSlides)
            ;

            _slides = this.WhenAnyValue(x => x.TitleSlide, x => x.StanzaSlides,
                (titleSlide, stanzaSlides) =>
                {
                    var x = new ObservableCollection<Slide>();

                    if (titleSlide != null)
                        x.Add(titleSlide);

                    if (stanzaSlides != null)
                        x.AddRange(stanzaSlides);

                    return x;
                })
                .ToProperty(this, c => c.Slides)
            ;


        }

        public List<Slide> processSongStanzasToSlides(IReadOnlyCollection<SongStanza> stanzas)
        {
            List<Slide> slides = new List<Slide>();

            //slides.Add(new SongTitleSlide() { Title = Title, Copyright = Copyright });

            foreach (var _datum in stanzas)
            {
                // break slides by newlines
                string[] lines = _datum.Lyrics.Split(new string[] { Environment.NewLine + Environment.NewLine },
                           StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    slides.Add(new SongSlide(_datum) { Text = line });
                }
            }

            return slides;
        }

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
}