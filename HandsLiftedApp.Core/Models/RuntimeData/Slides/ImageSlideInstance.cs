using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    public class ImageSlideInstance : ImageSlide, ISlideInstance
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public ImageSlideInstance(string imagePath, MediaGroupItem parentMediaGroupItem) : base(imagePath)
        {
            this.WhenAnyValue(s => s.SourceMediaFilePath) // todo dirty bit?
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });

            SlideTimerConfig = parentMediaGroupItem.AutoAdvanceTimer;

            // TODO
            // TODO
            // TODO
            // TODO
            // TODO
            // TODO
            parentMediaGroupItem.WhenAnyValue(x => x.AutoAdvanceTimer)
                .Subscribe(a => SlideTimerConfig = a);
        }

        private void GenerateBitmaps()
        {
            // MessageBus.Current.SendMessage(new SlideRenderRequestMessage(
            //     this,
            //     (obitmap) =>
            //     {
            var obitmap = BitmapLoader.LoadBitmap(SourceMediaFilePath);
            Cached = obitmap;
            Thumbnail = BitmapUtils.CreateThumbnail(obitmap);
            //     }
            // ));
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

        private ItemAutoAdvanceTimer? _SlideTimerConfig = null;

        public ItemAutoAdvanceTimer? SlideTimerConfig
        {
            get => _SlideTimerConfig;
            set => this.RaiseAndSetIfChanged(ref _SlideTimerConfig, value);
        }
    }
}