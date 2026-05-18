using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Core.Converters
{
	/// <summary>
	/// Multi-value converter that returns null when HasMotionBackground is true,
	/// or converts the file path to a bitmap when HasMotionBackground is false.
	/// Values[0] = HasMotionBackground (bool)
	/// Values[1] = BackgroundGraphicFilePath (string)
	/// </summary>
	public class MotionBackgroundImageConverter : IMultiValueConverter
	{
		public static readonly MotionBackgroundImageConverter Instance = new();

		private static readonly BitmapAssetValueConverter BitmapConverter = new();

		public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values == null || values.Count < 2)
				return null;

			bool hasMotionBackground = values[0] is true;

			if (hasMotionBackground)
				return null;

			// Delegate to the existing BitmapAssetValueConverter for the file path
			// Pass IImageBrushSource as targetType since this is used as an ImageBrush.Source
			return BitmapConverter.Convert(values[1], typeof(Avalonia.Media.IImageBrushSource), parameter, culture);
		}
	}
}
