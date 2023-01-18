using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    //[XmlType(TypeName = "SlidesGroupItemX")]
    [XmlRoot("SlidesGroupItem", Namespace = Constants.Namespace, IsNullable = false)]

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

        /// <summary>
        /// mutates *this* SlidesGroupItem and then returns a *new* SlidesGroupItem
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public SlidesGroupItem<I, J>? slice(int start)
        {
            if (start == 0)
            {
                return null;
            }

            SlidesGroupItem<I, J> slidesGroup = new SlidesGroupItem<I, J>() { Title = $"{Title} (Split copy)" };

            // TODO optimise below to a single loop
            // tricky bit: ensure index logic works whilst removing at the same time

            for (int i = start; i < _Slides.Count; i++)
            {
                slidesGroup._Slides.Add(_Slides[i]);
            }

            var count = _Slides.Count;
            for (int i = start; i < count; i++)
            {
                _Slides.RemoveAt(_Slides.Count - 1);
            }


            return slidesGroup;
        }
    }

}
