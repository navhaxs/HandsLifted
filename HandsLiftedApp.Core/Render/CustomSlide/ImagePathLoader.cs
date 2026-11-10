using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Core.Render.CustomSlide
{
    /// <summary>
    /// Loads an image path into whatever shows it, and does it again each time the path changes.
    ///
    /// Why this exists: <c>BitmapAssetValueConverter</c> only returns an <em>already cached</em> bitmap for a file
    /// path (decoding on the UI thread would stall it) and relies on the caller to start the real load. A
    /// binding to the path therefore shows nothing for a file that has not been loaded yet, and a later change of
    /// the path never loads anything unless something calls this again.
    /// </summary>
    internal static class ImagePathLoader
    {
        /// <param name="path">The path to show (null/empty clears the image).</param>
        /// <param name="currentPath">Returns the path now wanted; a result for a path that has since changed is dropped.</param>
        /// <param name="apply">Receives the bitmap (null when there is none or the file could not be loaded). Runs on the UI thread.</param>
        public static void Load(string? path, Func<string?> currentPath, Action<Bitmap?> apply)
        {
            if (string.IsNullOrEmpty(path))
            {
                apply(null);
                return;
            }

            // in-memory assets and files already decoded: no need to leave the UI thread
            if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                apply(BitmapLoader.LoadBitmap(path));
                return;
            }

            var cached = BitmapLoader.Cache.GetBitmap(path);
            if (cached != null)
            {
                apply(cached);
                return;
            }

            _ = BitmapLoader.LoadBitmapAsync(path).ContinueWith(
                t =>
                {
                    var bitmap = t.Result;
                    Dispatcher.UIThread.Post(
                        () =>
                        {
                            if (currentPath() == path) apply(bitmap);
                        },
                        DispatcherPriority.Background);
                },
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
