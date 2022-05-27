using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.Render;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace HandsLiftedApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Greeting => @"I wait upon You now
Lord I wait upon You now
Let Your presence fill me now
As I wait upon You now";

        public ObservableCollection<SlideState> Slides { get; set; }

        public int SlidesSelectedIndex { get; set; }

        SlideState _slidesSelectedItem;

        public SlideState SlidesSelectedItem
        {
            get => _slidesSelectedItem;
            set {
                this.RaiseAndSetIfChanged(ref _slidesSelectedItem, value);
                this.RaisePropertyChanged("Text");
                OnPropertyChanged("Text");

                OnPropertyChanged("NextSlide");
                OnPropertyChanged("SlidesNextItem");
            }
        }

        SlideState? _nextSlide;

        public SlideState? NextSlide
        {
            get => _nextSlide;
            set {
                this.RaiseAndSetIfChanged(ref _nextSlide, value);
            }
        }

        public SlideState SlidesNextItem
        {
            get => _slidesSelectedItem;
        }

        public string Text
        {
            get
            {
                return SlidesSelectedItem is SongSlideState ? ((SongSlideState)SlidesSelectedItem).Data.SlideText : "No slide selected yet";
            }
        }

        public OverlayState OverlayState { get; set; }

        public Boolean IsFrozen { get; set; }

        public SongSlide Slide { get; set; } = new SongSlide() { Text = "Path=slide from View Model" };

        //public String Slide { get; set; } = "hello this is my slide text set from the ViewModel";

        public MainWindowViewModel()
        {
            this.WhenAnyValue(t => t.SlidesSelectedItem).Subscribe(s => {
                this.RaisePropertyChanged("Text");
                OnPropertyChanged("Text");
                this.RaisePropertyChanged("SlidesSelectedItem");
                OnPropertyChanged("SlidesSelectedItem");

                var nextSlideState = Slides != null && SlidesSelectedIndex + 1 < Slides.Count ? Slides[SlidesSelectedIndex + 1] : null;
                if (nextSlideState != null)
                    NextSlide = nextSlideState;
            });

            var m = new ObservableCollection<Slide>();


            // Verse 1
            m.Add(new SongSlide { Text = "In the darkness we were waiting\nWithout hope without light\nTill from Heaven You came running\nThere was mercy in Your eyes" });
            m.Add(new SongSlide { Text = "To fulfil the law and prophets\nTo a virgin came the Word\nFrom a throne of endless glory\nTo a cradle in the dirt" });


            // C
            m.Add(new SongSlide { Text = "Praise the Father\nPraise the Son\nPraise the Spirit three in one" });
            m.Add(new SongSlide { Text = "God of Glory\nMajesty\nPraise forever to the King of kings" });


            // Verse 2
            m.Add(new SongSlide
            {
                Text = "To reveal the kingdom coming\nAnd to reconcile the lost\nTo redeem the whole creation\nYou did not despise the cross"
            });

            m.Add(new SongSlide
            {
                Text = "For even in Your suffering\nYou saw to the other side\nKnowing this was our salvation\nJesus for our sake You died"
            });


            //Verse 3
            m.Add(new SongSlide
            {
                Text = "And the morning that You rose\nAll of heaven held its breath\nTill that stone was moved for good\nFor the Lamb had conquered death"
            });

            m.Add(new SongSlide
            {
                Text = "And the dead rose from their tombs\nAnd the angels stood in awe\nFor the souls of all who'd come\nTo the Father are restored"
            });


            //Verse 4
            m.Add(new SongSlide
            {
                Text = "And the Church of Christ was born\nThen the Spirit lit the flame\nNow this Gospel truth of old\nShall not kneel shall not faint"
            });

            m.Add(new SongSlide
            {
                Text = "By His blood and in His Name\nIn His freedom I am free\nFor the love of Jesus Christ\nWho has resurrected me"
            });

            try
            {
                var images = Directory.GetFiles(@"C:\VisionScreens\TestImages", "*.*", SearchOption.AllDirectories)
                                .Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"));
                foreach (var f in images)
                {
                    if (f.EndsWith(".mp4"))
                    {
                        m.Add(new VideoSlide(f));
                    }
                    else {
                        m.Add(new ImageSlide(f));
                    }
                }
            }
            catch { }




            var xxx = m.Select(s => convertDataToState(s)).ToList();
            Slides = new ObservableCollection<SlideState>(xxx);
        }

        SlideState convertDataToState(Slide slide)
        {
            switch (slide)
            {
                case SongSlide d:
                    return new SongSlideState(d);
                case VideoSlide d:
                    return new VideoSlideState(d);
                case ImageSlide d:
                    return new ImageSlideState(d);
                default:
                    throw new Exception("error");
                    break;
            }
        }
    }
}
