using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Common;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.Services;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    public class ImageSlideInstance : ImageSlide, ISlideInstance, IRenderable, IDisposable
    {
        private DebounceDispatcher debounceDispatcher = new(200);
        private static readonly object loadDataLock = new object();

        // Cached comes from the shared, path-keyed BitmapLoader.Cache - this lease is what makes
        // it safe to eventually dispose that shared bitmap once nothing still references it (see
        // Dispose() below and PlaylistInstance.DisposeSlideRenderResources).
        private BitmapCacheLease? _cachedLease;

        public ImageSlideInstance(string imagePath) : base(imagePath)
        {
            this.WhenAnyValue(s => s.SourceMediaFilePath) // todo dirty bit?
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });
        }

        private async Task GenerateBitmaps()
        {
            if (Cached is not null && Thumbnail is not null)
            {
                return;
            }

            var lease = await BitmapLoader.AcquireBitmapAsync(SourceMediaFilePath);
            _cachedLease = lease;
            Cached = lease?.Bitmap;
            Thumbnail = BitmapUtils.CreateThumbnail(Cached);
            ThumbnailRenderSize.Track(this);
        }

        // Called by the render queue when the thumbnail size bucket changed (see ThumbnailRenderSize):
        // re-makes the thumbnail from the already-loaded image at the new size.
        public void Render()
        {
            var source = Cached;
            if (source is null)
                return;

            Bitmap? thumb;
            try
            {
                thumb = BitmapUtils.CreateThumbnail(source);
            }
            catch (ObjectDisposedException)
            {
                return; // the slide was torn down while this was queued
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var oldThumbnail = Thumbnail;
                Thumbnail = thumb;
                oldThumbnail?.Dispose();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        // Releases this slide's lease on the shared, cached background bitmap so it can finally
        // be disposed once nothing else references it. Called generically by
        // PlaylistInstance.DisposeSlideRenderResources when the owning item/playlist is torn down.
        public void Dispose()
        {
            _cachedLease?.Release();
            _cachedLease = null;
        }

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

        public SlideThumbnailBadge? SlideThumbnailBadge { get; }
        public override void OnPreloadSlide()
        {
            base.OnPreloadSlide();

            _ = GenerateBitmaps();
        }
    }
}