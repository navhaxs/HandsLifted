using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using LibMpv.Thumbnailing;
using Serilog;

namespace HandsLiftedApp.Core.Utils;

public class MpvThumbnailImageLoader : IAsyncImageLoader
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache = new();

    public async Task<Bitmap?> ProvideImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !File.Exists(url))
            throw new FileNotFoundException("File does not exist.", url);

        var bitmap = await _cache.GetOrAdd(url, LoadAsync).ConfigureAwait(false);

        if (bitmap == null)
            _cache.TryRemove(url, out _);

        return bitmap;
    }

    private static Task<Bitmap?> LoadAsync(string path) =>
        MpvThumbnailExtractor.ExtractAsync(path, maxWidth: 1280, maxHeight: 720)
            .ContinueWith<Bitmap?>(t =>
            {
                if (t.IsCanceled || t.IsFaulted)
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[MpvThumbnailImageLoader] Failed to load thumbnail for {Path}", path);
                    return null;
                }
                return t.Result;
            });

    public void Dispose() { }
}
