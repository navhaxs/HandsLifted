using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
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

    // Each extraction spins up its own libmpv context; a screen of folder-collage tiles requests dozens
    // at once, and heavy concurrency makes mpv hand back undecoded (black) frames and time out.
    private static readonly SemaphoreSlim Concurrency = new(3);

    private static async Task<Bitmap?> LoadAsync(string path)
    {
        await Concurrency.WaitAsync().ConfigureAwait(false);
        try
        {
            return await MpvThumbnailExtractor.ExtractAsync(path, maxWidth: 1280, maxHeight: 720)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MpvThumbnailImageLoader] Failed to load thumbnail for {Path}", path);
            return null;
        }
        finally
        {
            Concurrency.Release();
        }
    }

    public void Dispose() { }
}
