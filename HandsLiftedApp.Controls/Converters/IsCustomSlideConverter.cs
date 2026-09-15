using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Data.Models.Slides;

namespace HandsLiftedApp.Converters
{
    // True for a Slide that is a CustomSlide (MediaGroupItem.SlideItem.SlideData) - the
    // only slide type editable via the Custom Slide Editor.
    public class IsCustomSlideConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is CustomSlide;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
