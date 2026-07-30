using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

            // Default theme here is just the constructor's own initial value — the caller
            // (ScriptureItemInstance) immediately overwrites Theme with the item's
            // ResolvedDesignTheme after constructing or reusing this slide instance.
            Theme = Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();

            // Only react to the Theme *reference* changing here (e.g. a Design switch swapping
            // in a different BaseSlideTheme object). Property-edit-triggered re-rendering on the
            // currently-assigned theme is now ScriptureItemInstance's job (see its own
            // ResolvedDesignTheme-property subscription), which repaginates rather than just
            // re-rendering stale Lines at a new font size. A slide instance can still exist
            // without an owning, paginating ScriptureItemInstance (e.g. constructed directly in a
            // test), so this reference-change path stays as the fallback trigger for those cases.
            this.WhenAnyValue(x => x.Theme)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RequestRender());

            this.WhenAnyValue(x => x.Text)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
        }

        private IReadOnlyList<ScriptureParagraphLine> _lines = Array.Empty<ScriptureParagraphLine>();
        public IReadOnlyList<ScriptureParagraphLine> Lines
        {
            get => _lines;
            set => this.RaiseAndSetIfChanged(ref _lines, value);
        }

        private void RequestRender()
            => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

        public void Render()
        {
            var spec = ScriptureParagraphSpecBuilder.Build(this);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            var thumb = BitmapUtils.CreateThumbnail(cached);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var oldCached = Cached;
                var oldThumbnail = Thumbnail;
                Cached = cached;
                Thumbnail = thumb;
                oldCached?.Dispose();
                oldThumbnail?.Dispose();
            }, Avalonia.Threading.DispatcherPriority.Background);
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
