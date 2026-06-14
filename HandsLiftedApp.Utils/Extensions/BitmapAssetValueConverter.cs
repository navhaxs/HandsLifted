using Avalonia.Data.Converters;
using Avalonia.Media;
using HandsLiftedApp.Utils;
using System.Globalization;
using Avalonia;

namespace HandsLiftedApp.Extensions
{
    /// <summary>
    /// <para>
    /// Converts a string path to a bitmap asset.
    /// </para>
    /// <para>
    /// The asset must be in the same assembly as the program. If it isn't,
    /// specify "avares://<assemblynamehere>/" in front of the path to the asset.
    /// </para>
    /// </summary>
    public class BitmapAssetValueConverter : IValueConverter
    {
        public static BitmapAssetValueConverter Instance = new BitmapAssetValueConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UnsetValueType || value == null)
                return null;

            if (value is string rawUri && (targetType == typeof(IImageBrushSource) || targetType == typeof(IImage)))
            {
                if (rawUri.Length == 0)
                    return null;

                // avares:// paths are fast in-memory assets — stay sync
                if (rawUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                    return BitmapLoader.LoadBitmap(rawUri);

                // File paths: return cached bitmap or null (async load will set Source directly)
                return BitmapLoader.Cache.GetBitmap(rawUri);
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
