using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using Material.Icons;

namespace HandsLiftedApp.Core.Converters
{
    public class SlideToSlideDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MediaGroupItem.SlideItem SlideItem)
            {
                return SlideItem.SlideData;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}