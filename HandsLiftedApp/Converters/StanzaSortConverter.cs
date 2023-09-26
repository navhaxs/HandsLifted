using Avalonia.Data;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using System;
using System.Globalization;
using System.Linq;

namespace HandsLiftedApp.Converters
{

    // Map a "Stanza collection" to a sorted "Stanza collection"
    public class StanzaSortConverter : IValueConverter
    {
        public static readonly ItemToIndexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TrulyObservableCollection<SongStanza>)
            {
                var item = ((TrulyObservableCollection<SongStanza>)value);


                // is there a better way to do this? probably.
                var x = item.ToList().OrderBy(stanza => stanza.Name).ToList();

                return new TrulyObservableCollection<SongStanza>(x);
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
