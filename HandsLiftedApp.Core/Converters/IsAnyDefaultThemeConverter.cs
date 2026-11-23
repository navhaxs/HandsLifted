using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    /// <summary>
    /// True when a theme is "marked as default" in any way the designer's text labels show: the general default, or the
    /// song, song-motion or scripture default (each with its playlist override). It delegates to those label converters,
    /// so the thumbnail's star badge and the labels cannot disagree.
    /// Values, in order: [0] the theme, [1] app default song theme Guid, [2] playlist song override Guid,
    /// [3] app default song-motion Guid, [4] playlist song-motion override, [5] app default scripture Guid,
    /// [6] playlist scripture override. Anything missing, null or not a Guid (an unset or errored binding source) counts as
    /// "no id": the result is false rather than an exception.
    /// </summary>
    public class IsAnyDefaultThemeConverter : IMultiValueConverter
    {
        private static readonly IsDefaultThemeConverter General = new();
        private static readonly IsDefaultSongThemeConverter Song = new();
        private static readonly IsDefaultSongMotionThemeConverter SongMotion = new();
        private static readonly IsDefaultScriptureThemeConverter Scripture = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count == 0 || values[0] is not BaseSlideTheme theme)
                return false;

            return Equals(General.Convert(theme, targetType, parameter, culture), true)
                   || IsDefaultFor(Song, theme, values, 1, targetType, parameter, culture)
                   || IsDefaultFor(SongMotion, theme, values, 3, targetType, parameter, culture)
                   || IsDefaultFor(Scripture, theme, values, 5, targetType, parameter, culture);
        }

        private static bool IsDefaultFor(IMultiValueConverter converter, BaseSlideTheme theme, IList<object?> values,
            int appIndex, Type targetType, object? parameter, CultureInfo culture)
        {
            var args = new object?[] { theme, GuidOrNull(values, appIndex), GuidOrNull(values, appIndex + 1) };
            return Equals(converter.Convert(args, targetType, parameter, culture), true);
        }

        // UnsetValue / BindingNotification / null all mean "no id".
        private static object? GuidOrNull(IList<object?> values, int index) =>
            index < values.Count && values[index] is Guid id ? id : null;
    }
}
