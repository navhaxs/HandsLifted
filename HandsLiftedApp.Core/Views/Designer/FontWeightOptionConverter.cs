using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;

namespace HandsLiftedApp.Core.Views.Designer
{

    public class FontWeightOptionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is XmlFontWeight fw)
            {
                if ((int)(FontWeight)fw == 0)
                {
                    return new XmlFontWeight();
                }
                return value;
            }
            if (value is int)
            {
                return Math.Max(100, (int)value);
            }
            return FontWeight.Normal;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (targetType == typeof(XmlFontWeight) && value is FontWeight fontWeight)
            {
                return new XmlFontWeight(fontWeight);
            }

            if (targetType == typeof(FontWeight) && value is FontWeight)
            {
                return value;
            }

            if (targetType == typeof(FontWeight) && value == null)
            {
                return FontWeight.Normal;
            }

            // try
            // {
            //     return FontWeight.TryParse(value);
            // }
            // catch (Exception)
            // {
            // }
            return FontWeight.Normal;
        }
    }
}
