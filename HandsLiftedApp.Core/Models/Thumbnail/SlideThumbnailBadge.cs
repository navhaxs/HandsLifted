using ReactiveUI;

namespace HandsLiftedApp.Core.Models.Thumbnail
{
    public class SlideThumbnailBadge : ReactiveObject
    {
        private string _label;
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }
        
        private string _colour;
        public string Colour
        {
            get => _colour;
            set => this.RaiseAndSetIfChanged(ref _colour, value);
        }
    }
}