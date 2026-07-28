using Avalonia.Media.Imaging;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    // No IRenderable / self-rendering yet — Phase 3 adds ScriptureSlideSpecBuilder
    // and wires up reactive rendering the way SongSlideInstance does.
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance
    {
        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;
        }

        private Bitmap? _cached;
        public Bitmap? Cached
        {
            get => _cached;
            set => this.RaiseAndSetIfChanged(ref _cached, value);
        }

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        public ItemAutoAdvanceTimer? SlideTimerConfig => null;

        public SlideThumbnailBadge? SlideThumbnailBadge => null;
    }
}
