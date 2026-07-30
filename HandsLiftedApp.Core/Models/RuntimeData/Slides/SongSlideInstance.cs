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

        private static BaseSlideTheme ResolveTheme(Guid designId, bool hasMotionBackground)
        {
            return Globals.Instance.MainViewModel?.Playlist?.ResolveSongTheme(designId, hasMotionBackground)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme
                   ?? new BaseSlideTheme();
        }

        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id,
            string? text = null, string? label = null)
            : base(parentSongItem, parentSongStanza, id)
        {
            // Set text/label BEFORE subscriptions so initial WhenAnyValue emissions
            // carry the correct values; Skip(1) suppresses those initial emissions.
            if (text != null) Text = text;
            if (label != null) Label = label;

            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // Motion background presence flips which playlist default applies when Design is
            // unset (Guid.Empty), so this must re-resolve Theme, not just re-render.
            parentSongItem?.WhenAnyValue(x => x.MotionBackgroundVideoPath)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty, parentSongItem?.HasMotionBackground ?? false);
                    RequestRender();
                });

            // If Design is unset, this slide is riding whichever playlist default applies -
            // re-resolve whenever the user repoints one of the three playlist defaults.
            //
            // Subscribing directly here would capture `this` and `parentSongItem` strongly, and
            // Playlist.DefaultThemeAssignmentsChanged (an app-lifetime observable whose subscriber
            // list is never cleared) would then hold both alive for the life of the process - unlike
            // this class's other subscriptions, which are rooted in parentSongItem and die together
            // with it. Capturing only weak references lets the slide and its parent be garbage
            // collected normally; the subscription becomes a silent no-op once they're gone.
            var weakSelf = new WeakReference<SongSlideInstance>(this);
            var weakParentSongItem = parentSongItem != null ? new WeakReference<SongItemInstance>(parentSongItem) : null;
            Globals.Instance.MainViewModel?.Playlist?.DefaultThemeAssignmentsChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if (!weakSelf.TryGetTarget(out var self)) return;
                    var parent = weakParentSongItem != null && weakParentSongItem.TryGetTarget(out var p) ? p : null;
                    if ((parent?.Design ?? Guid.Empty) == Guid.Empty)
                    {
                        self.Theme = ResolveTheme(Guid.Empty, parent?.HasMotionBackground ?? false);
                        self.RequestRender();
                    }
                });

            this.WhenAnyValue(x => x.Theme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RequestRender());

            this.WhenAnyValue(x => x.Text)
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
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
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
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