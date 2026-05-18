using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Converters;
using Avalonia.Media;

namespace HandsLiftedApp.Core.Converters
{
	/// <summary>
	/// Multi-value converter that returns Transparent when HasMotionBackground is true,
	/// or converts the theme colour to a brush when HasMotionBackground is false.
	/// Values[0] = HasMotionBackground (bool)
	/// Values[1] = BackgroundAvaloniaColour (Color)
	/// </summary>
	public class MotionBackgroundBrushConverter : IMultiValueConverter
	{
		public static readonly MotionBackgroundBrushConverter Instance = new();

		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values == null || values.Count < 2)
				return Brushes.Black;

			if (values[0] == AvaloniaProperty.UnsetValue || values[0] == null)
				return Brushes.Black;

			bool hasMotionBackground = values[0] is true;

			if (hasMotionBackground)
				return Brushes.Transparent;

			if (values[1] is Color color)
				return ColorToBrushConverter.Convert(color, typeof(IBrush));

			return Brushes.Black;
		}
	}
}
