using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;

namespace HandsLiftedApp.Utils
{
    public static class BitmapLoader
    {
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
                        Log.Verbose($"Loading image {pathOrUri}");
                        return Bitmap.DecodeToWidth(imageStream, 1920);
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
