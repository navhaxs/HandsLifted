using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Common;
using Serilog;

namespace HandsLiftedApp.Utils
{
    public static class BitmapLoader
    {
        public static BitmapCache Cache = new(20);

        // Serializes concurrent loads of the same file path so two near-simultaneous callers
        // (e.g. multiple slides sharing one background image) can't both miss Cache and decode
        // the same file independently - the check-cache/decode/add-to-cache sequence below isn't
        // atomic on its own.
        private static readonly ConcurrentDictionary<string, object> PathLocks = new();

        public static Task<Bitmap?> LoadBitmapAsync(string pathOrUri, int? decodeToWidth = null)
        {
            return Task.Run(() => LoadBitmap(pathOrUri, decodeToWidth));
        }

        public static Bitmap? LoadBitmap(string pathOrUri, int? decodeToWidth = null)
        {
            try
            {
                Uri uri;

                // Allow for assembly overrides
                if (pathOrUri.StartsWith("avares://"))
                {
                    uri = new Uri(pathOrUri);
                }
                else
                {
                    //string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
                    //uri = new Uri($"avares://{assemblyName}{rawUri}");

                    // TODO: support file:///

                    if (!File.Exists(pathOrUri))
                        return null;

                    var pathLock = PathLocks.GetOrAdd(pathOrUri, _ => new object());
                    lock (pathLock)
                    {
                        var cached = Cache.GetBitmap(pathOrUri);
                        if (cached != null)
                        {
                            return cached;
                        }

                        var loaded = DecodeFromDisk(pathOrUri);
                        Cache.AddBitmap(pathOrUri, loaded);
                        return loaded;
                    }
                    //return new Bitmap(rawUri);
                }

                if (decodeToWidth != null)
                {
                    return Bitmap.DecodeToWidth(AssetLoader.Open(uri), decodeToWidth.Value);
                }

                return new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load image {pathOrUri}");
                return null;
            }
        }

        // For long-lived, explicitly-torn-down consumers (currently: ImageSlideInstance) that need
        // eviction to actually free native memory. The returned lease MUST be released once the
        // caller no longer needs the bitmap (e.g. when its owning slide instance is torn down) -
        // see PlaylistInstance.DisposeSlideRenderResources.
        public static Task<BitmapCacheLease?> AcquireBitmapAsync(string pathOrUri)
        {
            return Task.Run(() => AcquireBitmap(pathOrUri));
        }

        public static BitmapCacheLease? AcquireBitmap(string pathOrUri)
        {
            try
            {
                if (pathOrUri.StartsWith("avares://"))
                {
                    // Never cached/shared - a fresh decode here is exclusively owned by the
                    // caller, so releasing the lease can just dispose it directly.
                    var bitmap = LoadBitmap(pathOrUri);
                    return bitmap == null ? null : new BitmapCacheLease(bitmap, bitmap.Dispose);
                }

                if (!File.Exists(pathOrUri))
                    return null;

                var pathLock = PathLocks.GetOrAdd(pathOrUri, _ => new object());
                lock (pathLock)
                {
                    var leased = Cache.Acquire(pathOrUri);
                    if (leased != null)
                    {
                        return leased;
                    }

                    var loaded = DecodeFromDisk(pathOrUri);
                    return Cache.AddAndAcquire(pathOrUri, loaded);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load image {pathOrUri}");
                return null;
            }
        }

        private static Bitmap DecodeFromDisk(string path)
        {
            Log.Verbose($"Loading image {path} - fresh load");
            using Stream imageStream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(imageStream, 1920);
        }
    }
}