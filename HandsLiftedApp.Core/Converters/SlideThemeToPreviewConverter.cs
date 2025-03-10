using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class SlideThemeToPreviewConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BaseSlideTheme baseSlideTheme)
            {
                return new SongSlideInstance(null, null, null)
                {
                    // SlideTheme = baseSlideTheme,
                    Text = @"Shine Jesus shine
Fill this land
With the Father's glory
Blaze Spirit blaze
Set our hearts on fire"
                };
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}