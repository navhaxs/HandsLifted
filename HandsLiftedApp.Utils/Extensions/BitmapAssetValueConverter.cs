﻿using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using System;
using System.Globalization;
using System.IO;

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
            if (value == null)
                return null;

            if (value is string rawUri && (targetType == typeof(IBitmap) || targetType == typeof(IImage)))
            {
                if (rawUri.Length == 0)
                    return null;


                try {


                    Uri uri;

                    // Allow for assembly overrides
                    if (rawUri.StartsWith("avares://")) {
                        uri = new Uri(rawUri);
                    }
                    else {
                        //string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
                        //uri = new Uri($"avares://{assemblyName}{rawUri}");

                        if (!File.Exists(rawUri))
                            return null;

                        using (Stream imageStream = File.OpenRead(rawUri)) {
                            return Bitmap.DecodeToWidth(imageStream, 400);
                        }
                        //return new Bitmap(rawUri);
                    }

                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    var asset = assets.Open(uri);

                    return new Bitmap(asset);
                }
                catch (Exception ex) {
                    Log.Error($"Failed to load image {value}");
                    return null;
                }
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
