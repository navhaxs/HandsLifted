using System;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Models.Slides;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    public class CustomAxamlSlideInstance : CustomAxamlSlide //, ISlideInstance
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public CustomAxamlSlideInstance(string axamlFilePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png") : base()
        {
            this.SourceMediaFilePath = axamlFilePath;
            // this.WhenAnyValue(s => s.SourceMediaFilePath) // todo dirty bit?
            //     .ObserveOn(RxApp.MainThreadScheduler)
            //     .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });
        }

        private void GenerateBitmaps()
        {
            // MessageBus.Current.SendMessage(new SlideRenderRequestMessage(
            //     this,
            //     (obitmap) =>
            //     {
            // var obitmap = BitmapLoader.LoadBitmap(SourceMediaFilePath); 
            //         Cached = obitmap;
            //         Thumbnail = BitmapUtils.CreateThumbnail(obitmap);
            //     }
            // ));
        }
        //
        // Bitmap _cached;
        //
        // public Bitmap? Cached
        // {
        //     get => _cached;
        //     set => this.RaiseAndSetIfChanged(ref _cached, value);
        // }
        //
        // Bitmap _thumbnail;
        //
        // public Bitmap? Thumbnail
        // {
        //     get => _thumbnail;
        //     set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        // }
    }
}