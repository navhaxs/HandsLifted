using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using Serilog;

namespace HandsLiftedApp.Utils
{
    public static class BitmapLoader
    {
        public static Bitmap LoadBitmap(string pathOrUri)
        {
            return null;
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
                        return Bitmap.DecodeToWidth(imageStream, 1920);
                    }
                    //return new Bitmap(rawUri);
                }

                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var asset = assets.Open(uri);

                return new Bitmap(asset);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load image {pathOrUri}");
                return null;
            }
        }
    }
}
