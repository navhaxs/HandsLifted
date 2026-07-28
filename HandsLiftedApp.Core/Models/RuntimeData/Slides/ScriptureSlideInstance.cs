using Avalonia.Media.Imaging;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Data.Slides
{
    public class ScriptureSlideInstance : ScriptureSlide, ISlideInstance, IRenderable
    {
        private readonly DebounceDispatcher debounceDispatcher = new(200);

        public ScriptureSlideInstance(ScriptureItem? parentScriptureItem, string id, string? text = null, string? label = null)
            : base(parentScriptureItem, id)
        {
            if (text != null) Text = text;
            if (label != null) Label = label;

            // No per-item Design/theme-selection concept exists yet for scripture items
            // (unlike SongItem.Design) — every scripture slide uses the app's default theme.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();

            this.WhenAnyValue(x => x.Theme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());

            this.WhenAnyValue(x => x.Text)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
        }

        private void RequestRender()
            => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

        public void Render()
        {
            var spec = ScriptureSlideSpecBuilder.Build(this);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            var thumb = BitmapUtils.CreateThumbnail(cached);
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { Cached = cached; Thumbnail = thumb; },
                Avalonia.Threading.DispatcherPriority.Background);
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
