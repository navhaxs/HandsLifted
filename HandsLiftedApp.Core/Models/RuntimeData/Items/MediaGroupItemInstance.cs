using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class MediaGroupItemInstance : MediaGroupItem, IItemInstance
    {
        public PlaylistInstance ParentPlaylist { get; set; }

        private BlankSlide _blankSlide = new BlankSlide();

        public MediaGroupItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides, (selectedSlideIndex, slides) =>
                {
                    try
                    {
                        if (selectedSlideIndex > -1 && selectedSlideIndex < slides.Count)
                        {
                            return slides.ElementAt(selectedSlideIndex);
                        }
                    }
                    catch (System.Exception _ignored) { }

                    return _blankSlide;
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

        }

        public void GenerateSlides()
        {
            var x = new List<Slide>();
            foreach (var item in Items)
            {
                var generateMediaContentSlide = CreateItem.GenerateMediaContentSlide(item);
                x.Add(generateMediaContentSlide);
            }

            _Slides = x;

            this.RaisePropertyChanged(nameof(Slides));
        }

        public List<Slide> _Slides = new List<Slide>();
        public ObservableCollection<Slide> Slides => new(_Slides);

        public int _selectedSlideIndex = -1;

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