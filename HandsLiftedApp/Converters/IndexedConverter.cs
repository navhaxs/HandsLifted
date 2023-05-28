using Avalonia;
using Avalonia.Data.Converters;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using HandsLiftedApp.Models.Data;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Converters
{
    public class IndexedConverter : IValueConverter
    {
        public object Convert
            (object value
            , Type targetType
            , object parameter
            , CultureInfo culture
            )
        {
            IEnumerable t = value as IEnumerable;
            if (t == null)
            {
                return null;
            }

            IEnumerable<object> e = t.Cast<object>();
            int i = 1;
            return new ObservableCollection<Indexed<object>>(e.Select(x => Indexed.Create(i++, x)));
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
