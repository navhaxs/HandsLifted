using System;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using HandsLiftedApp.Core.Utils;
using LibMpv.Thumbnailing;
using Serilog;
using ShellThumbs;
using SkiaSharp;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlideInstance : SongTitleSlide, ISlideInstance, IRenderable
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        private static BaseSlideTheme ResolveTheme(Guid designId, bool hasMotionBackground)
        {
            return Globals.Instance.MainViewModel?.Playlist?.ResolveSongTheme(designId, hasMotionBackground)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme
                   ?? new BaseSlideTheme();
        }

        public SongTitleSlideInstance(SongItemInstance? parentSongItem) : base()
        {
            ParentSongItem = parentSongItem;
            Log.Verbose("Creating slide instance");
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
            // unset (Guid.Empty), so this must re-resolve Theme, not just re-render (this
            // subscription previously only re-rendered).
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
            var weakSelf = new WeakReference<SongTitleSlideInstance>(this);
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

            this.WhenAnyValue(s => s.Title, s => s.Copyright) // todo dirty bit?
                .Skip(1)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RequestRender());
        }

        private void RequestRender()
            => debounceDispatcher.Debounce(() => Globals.Instance.SlideRenderQueue.Enqueue(this));

        public void Render()
        {
            SKBitmap? videoFrame = null;
            if (HasMotionBackground)
            {
                var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
                if (!string.IsNullOrWhiteSpace(videoPath))
                {
                    if (ThumbnailEngineSettings.UseMpvEngine)
                    {
                        try
                        {
                            using var avaBmp = MpvThumbnailExtractor.ExtractAsync(videoPath, maxWidth: 1920, maxHeight: 1080)
                                .GetAwaiter().GetResult();
                            if (avaBmp != null)
                                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[SongTitleSlideInstance] Failed to extract video thumbnail from {Path}", videoPath);
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                                videoPath, 1920, 1080, ThumbnailOptions.None);
                            if (avaBmp != null)
                                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[SongTitleSlideInstance] Failed to extract video thumbnail from {Path}", videoPath);
                        }
                    }
                }
            }

            var spec = SongTitleSlideSpecBuilder.Build(this, videoFrame);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            videoFrame?.Dispose();
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

        // refs
        public SongItemInstance? ParentSongItem { get; }

        [System.Xml.Serialization.XmlIgnore]
        public bool HasMotionBackground => ParentSongItem?.HasMotionBackground ?? false;

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
        
        public SlideThumbnailBadge? SlideThumbnailBadge { get; }
    }
}