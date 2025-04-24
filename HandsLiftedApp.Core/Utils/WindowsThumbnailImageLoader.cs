using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using ShellThumbs;

namespace HandsLiftedApp.Core.Utils;

public class WindowsThumbnailImageLoader : IAsyncImageLoader
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _memoryCache = new();
    
    private Task<Bitmap?> LoadAsync(string url)
    {
        return Task.Run(() =>
        {
            try
            {
                var bitmap = WindowsThumbnailProvider.GetThumbnail(url, 1280, 720, ThumbnailOptions.None);
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading thumbnail: {ex.Message}");
            }
            return null;
        });
    }
    
    public async Task<Bitmap?> ProvideImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !File.Exists(url))
        {
            throw new FileNotFoundException("The specified file does not exist.", url);
        }

        var bitmap = await _memoryCache.GetOrAdd(url, LoadAsync).ConfigureAwait(false);

        // If load failed - remove from cache and return
        if (bitmap == null)
        {
            _memoryCache.TryRemove(url, out _);
        }

        return bitmap;
    }
    
    public void Dispose()
    {
    }
}