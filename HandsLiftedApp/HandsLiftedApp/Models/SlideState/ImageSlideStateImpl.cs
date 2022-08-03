using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.IO;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class ImageSlideStateImpl : SlideStateBase<ImageSlide<ImageSlideStateImpl>>, IImageSlideState
    {
        public ImageSlideStateImpl(ref ImageSlide<ImageSlideStateImpl> imageSlide) : base(ref imageSlide)
        {
            _ = LoadImage();
        }
        public async Task LoadImage()
        {
            Image = await Task.Run(() =>
            {
                var path = _slide.ImagePath;

                if (File.Exists(path))
                {
                    using (Stream imageStream = File.OpenRead(path))
                    {
                        return Bitmap.DecodeToWidth(imageStream, 1920);
                    }
                }

                return null;
            });
        }

        public string ImageName
        {
            get
            {
                return Path.GetFileName(_slide.ImagePath);
            }
        }

        private Bitmap? _image;
        public Bitmap? Image
        {
            get => _image;
            private set => this.RaiseAndSetIfChanged(ref _image, value);
        }
    }
}
