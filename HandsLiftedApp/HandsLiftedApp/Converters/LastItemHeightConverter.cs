using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace HandsLiftedApp.Converters
{

    public class LastItemHeightConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values[1] is ItemsControl)
            {
                var item = ((ItemsControl)values[1]);
                if (item.ItemContainerGenerator.Containers.Count() == 0)
                {
                    return values[0];
                }
                var last = item.ItemContainerGenerator.Containers.Last();
                var lastItemContainerHeight = last.ContainerControl.Bounds.Height;
                return (double)values[0] - lastItemContainerHeight;
            }

            // converter used for the wrong type
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
