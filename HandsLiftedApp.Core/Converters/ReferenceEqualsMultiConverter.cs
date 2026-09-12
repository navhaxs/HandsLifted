using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Core.Converters
{
    public class ReferenceEqualsMultiConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, System.Type targetType, object? parameter, CultureInfo culture)
        {
            return values.Count == 2 && ReferenceEquals(values[0], values[1]);
        }
    }
}
