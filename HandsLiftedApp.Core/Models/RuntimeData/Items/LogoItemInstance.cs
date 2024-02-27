using System.Collections.ObjectModel;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public class LogoItemInstance : LogoItem, IItemInstance
    {
        public PlaylistInstance ParentPlaylist { get; set; }

        public LogoItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
        }

        private int _selectedSlideIndex = -1;
        public int SelectedSlideIndex { get => _selectedSlideIndex; set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value); }
        private LogoSlide _logoSlide = new();
        public Slide ActiveSlide { get => _logoSlide; }
        public ObservableCollection<Slide> Slides => new() { _logoSlide };

    }
}