using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Core.Converters
{
    public class FilePathToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is String inputString)
            {
                try
                {
                    return Path.GetFileName(inputString);
                }
                catch (Exception)
                {
                    return inputString;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}