using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Common;
using Serilog;

namespace HandsLiftedApp.Utils
{
    public static class BitmapLoader
    {
        public static BitmapCache Cache = new(20);

        public static Bitmap LoadBitmap(string pathOrUri)
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

                    using (Stream imageStream = File.OpenRead(pathOrUri))
                    {
                        var cached = Cache.GetBitmap(pathOrUri);
                        if (cached != null)
                        {
                            Log.Verbose($"Loading image {pathOrUri} - cache hit");
                            return cached;
                        }

                        Log.Verbose($"Loading image {pathOrUri} - fresh load");
                        var loaded = Bitmap.DecodeToWidth(imageStream, 1920);
                        Cache.AddBitmap(pathOrUri, loaded);
                        return loaded;
                    }
                    //return new Bitmap(rawUri);
                }

                return new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load image {pathOrUri}");
                return null;
            }
        }
    }
}