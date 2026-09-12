using System;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Core.Utils
{
    public class ThumbnailImageLoader : IAsyncImageLoader
    {
        private readonly IAsyncImageLoader _videoLoader;

        public ThumbnailImageLoader(IAsyncImageLoader videoLoader)
        {
            _videoLoader = videoLoader;
        }

        public Task<Bitmap?> ProvideImageAsync(string url)
        {
            var ext = Path.GetExtension(url).TrimStart('.').ToLowerInvariant();
            if (Array.IndexOf(Constants.SUPPORTED_VIDEO, ext) >= 0)
                return _videoLoader.ProvideImageAsync(url);

            return BitmapLoader.LoadBitmapAsync(url, decodeToWidth: 1280);
        }

        public void Dispose()
        {
            _videoLoader.Dispose();
        }
    }
}
