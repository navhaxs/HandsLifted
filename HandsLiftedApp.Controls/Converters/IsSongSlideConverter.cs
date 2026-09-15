using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Converters
{
    // True for a Slide that belongs to a Song item (SongSlide/SongTitleSlide base types -
    // SongSlideInstance/SongTitleSlideInstance derive from these). None of the per-slide
    // context menu actions (Edit/Change Media/Duplicate/Delete) apply to these - they only
    // make sense for slides backed by a MediaGroupItem.GroupItem.
    public class IsSongSlideConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is SongSlide or SongTitleSlide;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
