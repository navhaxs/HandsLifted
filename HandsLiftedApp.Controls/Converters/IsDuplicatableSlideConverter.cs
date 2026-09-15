using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Converters
{
    // True for a Slide backed by a MediaGroupItem.GroupItem (MediaItem -> MediaSlide,
    // SlideItem -> CustomSlide) - the only slides that can be duplicated in-place.
    public class IsDuplicatableSlideConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is MediaSlide or CustomSlide;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
