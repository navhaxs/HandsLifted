using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Converters
{
    // True for a Slide with a changeable source file (ImageSlide/VideoSlide, generated from
    // a MediaGroupItem.MediaItem's SourceMediaFilePath - see CreateItem.GenerateMediaContentSlide).
    public class IsMediaBackedSlideConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is MediaSlide;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
