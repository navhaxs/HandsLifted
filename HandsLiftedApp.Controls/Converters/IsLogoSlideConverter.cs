using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Converters
{
    // True for the synthetic LogoSlide shown as the thumbnail of a LogoItem. It is not backed by
    // a MediaGroupItem.GroupItem, so the per-slide actions don't apply - only whole-item actions do.
    public class IsLogoSlideConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is LogoSlide;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
