using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    public class SlidesGroupItem<I, J> : Item<I> where I : IItemState where J : IItemAutoAdvanceTimerState
    {

        private ObservableCollection<Slide> _internal_slides = new ObservableCollection<Slide>();


        private J _timerState;
        [XmlIgnore]
        public J TimerState { get => _timerState; set => this.RaiseAndSetIfChanged(ref _timerState, value); }

        public SlidesGroupItem()
        {
            TimerState = (J)Activator.CreateInstance(typeof(J), this);
        
            _Slides.CollectionChanged += (s, x) =>
            {
                this.RaisePropertyChanged(nameof(Slides));
            };
        }

        [XmlIgnore] // TODO
        public ObservableCollection<Slide> _Slides { get => _internal_slides; set => this.RaiseAndSetIfChanged(ref _internal_slides, value); }
        [XmlIgnore]
        public override ObservableCollection<Slide> Slides { get => _Slides; }

        private bool _IsLooping = false;
        /// <summary>
        /// Loop back to the first slide of the item once reaching the end 
        /// </summary>
        public bool IsLooping { get => _IsLooping; set => this.RaiseAndSetIfChanged(ref _IsLooping, value); }

        private ItemAutoAdvanceTimer _AutoAdvanceTimer = new ItemAutoAdvanceTimer();
        public ItemAutoAdvanceTimer AutoAdvanceTimer { get => _AutoAdvanceTimer; set => this.RaiseAndSetIfChanged(ref _AutoAdvanceTimer, value); }
    }

}
