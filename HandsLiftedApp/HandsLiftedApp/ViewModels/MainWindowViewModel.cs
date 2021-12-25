using HandsLiftedApp.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;

namespace HandsLiftedApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Greeting => @"I wait upon You now
Lord I wait upon You now
Let Your presence fill me now
As I wait upon You now";

        public ObservableCollection<Slide> Slides { get; set; }

        public int SlidesSelectedIndex { get; set; }

        SongSlide _slidesSelectedItem;

        public SongSlide SlidesSelectedItem
        {
            get => _slidesSelectedItem;
            set {
                this.RaiseAndSetIfChanged(ref _slidesSelectedItem, value);
                this.RaisePropertyChanged("Text");
                OnPropertyChanged("Text");
            }
        }

        public string Text
        {
            get
            {
                return SlidesSelectedItem != null ? SlidesSelectedItem.Text : "hello";
            }
        }

        public OverlayState OverlayState { get; set; }

        public Boolean IsFrozen { get; set; }

        public MainWindowViewModel()
        {
            this.WhenAnyValue(t => t.SlidesSelectedItem).Subscribe(s => {
                this.RaisePropertyChanged("Text");
                OnPropertyChanged("Text");
            });

            Slides = new ObservableCollection<Slide>();


            // Verse 1
            Slides.Add(new SongSlide { Text = "In the darkness we were waiting\nWithout hope without light\nTill from Heaven You came running\nThere was mercy in Your eyes" });
            Slides.Add(new SongSlide { Text = "To fulfil the law and prophets\nTo a virgin came the Word\nFrom a throne of endless glory\nTo a cradle in the dirt" });


            // C
            Slides.Add(new SongSlide { Text = "Praise the Father\nPraise the Son\nPraise the Spirit three in one" });
            Slides.Add(new SongSlide { Text = "God of Glory\nMajesty\nPraise forever to the King of kings" });


            // Verse 2
            Slides.Add(new SongSlide
            {
                Text = "To reveal the kingdom coming\nAnd to reconcile the lost\nTo redeem the whole creation\nYou did not despise the cross"
            });

            Slides.Add(new SongSlide
            {
                Text = "For even in Your suffering\nYou saw to the other side\nKnowing this was our salvation\nJesus for our sake You died"
            });


            //Verse 3
            Slides.Add(new SongSlide
            {
                Text = "And the morning that You rose\nAll of heaven held its breath\nTill that stone was moved for good\nFor the Lamb had conquered death"
            });

            Slides.Add(new SongSlide
            {
                Text = "And the dead rose from their tombs\nAnd the angels stood in awe\nFor the souls of all who'd come\nTo the Father are restored"
            });


            //Verse 4
            Slides.Add(new SongSlide
            {
                Text = "And the Church of Christ was born\nThen the Spirit lit the flame\nNow this Gospel truth of old\nShall not kneel shall not faint"
            });

            Slides.Add(new SongSlide
            {
                Text = "By His blood and in His Name\nIn His freedom I am free\nFor the love of Jesus Christ\nWho has resurrected me"
            });

            Slides.Add(new ImageSlide());
         
        }
    }
}
