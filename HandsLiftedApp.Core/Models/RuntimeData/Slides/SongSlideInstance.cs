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
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlideInstance : SongSlide, ISlideInstance
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

        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id) : base(
            parentSongItem, parentSongStanza, id)
        {
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

            debounceDispatcher.Debounce(() => GenerateBitmaps());

            this.WhenAnyValue(x => x.Text)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe((x) =>
                {
                    debounceDispatcher.Debounce(() => GenerateBitmaps());
                });
            
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

        private void GenerateBitmaps()
        {
            var spec = SongSlideSpecBuilder.Build(this);
            using var skBitmap = SlideRenderer.RenderToSKBitmap(spec);
            Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
            Thumbnail = BitmapUtils.CreateThumbnail(Cached);
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