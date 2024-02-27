using System.Collections.ObjectModel;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class BlankItemInstance : IItemInstance
    {
        public BlankItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
        }

        public PlaylistInstance ParentPlaylist { get; set; }
        public int SelectedSlideIndex { get; set; }
        private BlankSlide _blankSlide = new();

        public Slide ActiveSlide
        {
            get => _blankSlide;
        }

        public ObservableCollection<Slide> Slides => new() { _blankSlide };
    }
}