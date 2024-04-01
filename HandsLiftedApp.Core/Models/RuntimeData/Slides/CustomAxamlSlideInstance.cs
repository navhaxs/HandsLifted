using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Models.Slides;

namespace HandsLiftedApp.Core.Models.RuntimeData.Slides
{
    public class CustomAxamlSlideInstance : CustomAxamlSlide //, ISlideInstance
    {
        public MediaGroupItem.MediaItem parentMediaItem { get; set; }
        public CustomAxamlSlideInstance(MediaGroupItem.MediaItem parentMediaItem) : base()
        {
            this.parentMediaItem = parentMediaItem;
            this.SourceMediaFilePath = parentMediaItem.SourceMediaFilePath;
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