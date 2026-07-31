using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia.Media.Imaging;
using Serilog;
using ShellThumbs;

namespace HandsLiftedApp.Core.Utils;

public class WindowsThumbnailImageLoader : IAsyncImageLoader
{
    // Cloud-sync placeholders (e.g. Google Drive File Stream) that aren't hydrated yet fail the
    // shell thumbnail call with a COM/IO error indistinguishable in kind from a real failure -
    // the only signal is that it clears up once the filesystem finishes downloading the file.
    // Retry with backoff instead of giving up on the first attempt.
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
    };

    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _memoryCache = new();

    private static bool IsRetryable(Exception ex) => ex switch
    {
        // ShellThumbs already resolves the one known "permanent, no thumbnail exists" case
        // (WTS_E_FAILEDEXTRACTION) to a null return instead of throwing, so any COMException
        // that reaches here is something else - most commonly a cloud placeholder not ready.
        COMException => true,
        IOException => true,
        _ => false,
    };

    private async Task<Bitmap?> LoadAsync(string url)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await Task.Run(() => WindowsThumbnailProvider.GetThumbnail(url, 1280, 720, ThumbnailOptions.None))
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < RetryDelays.Length)
            {
                Log.Debug(ex, "Thumbnail not ready for {Url}, retry {Attempt}/{Max}", url, attempt + 1, RetryDelays.Length);
                await Task.Delay(RetryDelays[attempt]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading thumbnail for {Url}", url);
                return null;
            }
        }
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