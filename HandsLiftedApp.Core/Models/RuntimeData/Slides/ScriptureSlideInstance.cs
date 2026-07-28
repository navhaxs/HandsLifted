using Avalonia.Media.Imaging;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    // IRenderable / self-rendering added in Phase 3 Task 3, once ScriptureSlideSpecBuilder
    // (Task 2) exists for Render() to call.
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance
    {
        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;

            // No per-item Design/theme-selection concept exists yet for scripture items
            // (unlike SongItem.Design) — every scripture slide uses the app's default theme.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }

        private BaseSlideTheme? _theme;
        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
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
