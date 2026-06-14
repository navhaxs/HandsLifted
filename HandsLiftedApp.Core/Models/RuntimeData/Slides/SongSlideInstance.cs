using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Xml.Serialization;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlideInstance : SongSlide, ISlideInstance, IRenderable
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        private static BaseSlideTheme ResolveTheme(Guid designId)
        {
            if (designId != Guid.Empty)
            {
                var theme = Globals.Instance.MainViewModel?.Playlist?.Designs
                    .FirstOrDefault(d => d.Id == designId);
                if (theme != null) return theme;
            }
            return Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme();
        }

        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id,
            string? text = null, string? label = null)
            : base(parentSongItem, parentSongStanza, id)
        {
            // Set text/label BEFORE subscriptions so initial WhenAnyValue emissions
            // carry the correct values; Skip(1) suppresses those initial emissions.
            if (text != null) Text = text;
            if (label != null) Label = label;

            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId);
                    RequestRender();
                });

            this.WhenAnyValue(x => x.Theme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());

            this.WhenAnyValue(x => x.Text)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
            
            _calculatedSlideThumbnailBadge = this.WhenAnyValue(x => x.Label, x => x.ParentSongStanza,
                    (label, parentSongStanza) =>
                    {
                        if (label != null && label.Length > 0)
                        {
                            return new SlideThumbnailBadge() { Label = label, Colour = parentSongStanza.Colour };
                        }

                        return null;
                    })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .ToProperty(this, x => x.SlideThumbnailBadge);
        }

        private void RequestRender()
            => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

        public void Render()
        {
            var spec = SongSlideSpecBuilder.Build(this);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            var cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            var thumb = BitmapUtils.CreateThumbnail(cached);
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { Cached = cached; Thumbnail = thumb; },
                Avalonia.Threading.DispatcherPriority.Background);
        }

        [XmlIgnore]
        public bool HasMotionBackground => (ParentSongItem as SongItemInstance)?.HasMotionBackground ?? false;

        private BaseSlideTheme? _theme;
        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
        }

        // refs

        Bitmap _cached;

        public Bitmap? Cached
        {
            get => _cached;
            set => this.RaiseAndSetIfChanged(ref _cached, value);
        }
        
        Bitmap _thumbnail;

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }
        
        public ItemAutoAdvanceTimer? SlideTimerConfig => null;

        private readonly ObservableAsPropertyHelper<SlideThumbnailBadge> _calculatedSlideThumbnailBadge;
        public SlideThumbnailBadge? SlideThumbnailBadge => _calculatedSlideThumbnailBadge.Value;
    }
}