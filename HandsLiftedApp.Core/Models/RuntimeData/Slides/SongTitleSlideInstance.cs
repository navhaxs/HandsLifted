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
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using Serilog;
using ShellThumbs;
using SkiaSharp;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlideInstance : SongTitleSlide, ISlideInstance
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

        public SongTitleSlideInstance(SongItemInstance? parentSongItem) : base()
        {
            ParentSongItem = parentSongItem;
            Log.Verbose("Creating slide instance");
            Theme = ResolveTheme(parentSongItem?.Design ?? Guid.Empty);

            parentSongItem?.WhenAnyValue(x => x.Design)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(designId =>
                {
                    Theme = ResolveTheme(designId);
                    debounceDispatcher.Debounce(() => GenerateBitmaps());
                });

            this.WhenAnyValue(x => x.Theme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => debounceDispatcher.Debounce(() => GenerateBitmaps()));

            this.WhenAnyValue(s => s.Title, s => s.Copyright) // todo dirty bit?
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });

            debounceDispatcher.Debounce(() => GenerateBitmaps());
        }

        private void GenerateBitmaps()
        {
            SKBitmap? videoFrame = null;
            if (HasMotionBackground && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var videoPath = ParentSongItem?.MotionBackgroundVideoPath;
                if (!string.IsNullOrWhiteSpace(videoPath))
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

            var spec = SongTitleSlideSpecBuilder.Build(this, videoFrame);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            videoFrame?.Dispose();
            Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            Thumbnail = BitmapUtils.CreateThumbnail(Cached);
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