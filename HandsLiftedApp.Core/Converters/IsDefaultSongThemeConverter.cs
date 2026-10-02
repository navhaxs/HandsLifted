using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    // IMultiValueConverter so the binding subscribes directly to the current default id
    // (values[1]) rather than only to the theme item (values[0]) - setting
    // Playlist.DefaultSongThemeId raises PropertyChanged on the Playlist, not on the bound theme
    // item, so a single-value converter reading Globals.Instance.MainViewModel.Playlist internally
    // would never re-evaluate when the default changes live.
    public class IsDefaultSongThemeConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2) return false;
            if (values[0] is not BaseSlideTheme theme) return false;
            // values[2] (optional) is the playlist override; it beats the app default in values[1].
            Guid? effective = values.Count > 2 && values[2] is Guid p ? p
                : values[1] is Guid a ? a
                : null;
            return effective.HasValue && theme.Id == effective.Value;
        }
    }
}
