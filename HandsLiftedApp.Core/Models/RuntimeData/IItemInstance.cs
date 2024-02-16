using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public interface IItemInstance
    {
        public PlaylistInstance? ParentPlaylist { get; set; }
        public int SelectedSlideIndex { get; set; }
        public Slide ActiveSlide { get; }
    }
}