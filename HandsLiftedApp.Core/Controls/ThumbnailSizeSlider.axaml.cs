using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Controls
{
    public partial class ThumbnailSizeSlider : UserControl
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<ThumbnailSizeSlider, double>(nameof(Value), defaultValue: 80d, defaultBindingMode: BindingMode.TwoWay);

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Width of the panel the thumbnails are laid out in. When greater than 0 the slider snaps to
        /// column-count notches; at 0 (default) it behaves as the plain 50-150 slider.
        /// </summary>
        public static readonly StyledProperty<double> AvailableWidthProperty =
            AvaloniaProperty.Register<ThumbnailSizeSlider, double>(nameof(AvailableWidth), defaultValue: 0d);

        public double AvailableWidth
        {
            get => GetValue(AvailableWidthProperty);
            set => SetValue(AvailableWidthProperty, value);
        }

        private double[] _notches = Array.Empty<double>();

        static ThumbnailSizeSlider()
        {
            AvailableWidthProperty.Changed.AddClassHandler<ThumbnailSizeSlider>((s, _) => s.ApplyRange());
        }

        public ThumbnailSizeSlider()
        {
            InitializeComponent();
            ApplyRange();
        }

        private void ApplyRange()
        {
            var slider = this.FindControl<Slider>("ThumbnailSlider");
            if (slider == null) return;

            if (AvailableWidth <= 0)
            {
                _notches = Array.Empty<double>();
                slider.IsEnabled = true;
                slider.Ticks = null;
                slider.TickFrequency = 0.05;
                slider.IsSnapToTickEnabled = true;
                // Keep Minimum < Maximum at all times.
                slider.Maximum = 150;
                slider.Minimum = 50;
                return;
            }

            var itemWidth = Globals.Instance.MainViewModel?.ItemWidth ?? 290;
            _notches = SlideGridMetrics.ColumnNotches(AvailableWidth - SlideGridMetrics.PanelInsetWidth, itemWidth);
            var min = _notches[0];
            var max = _notches[^1];
            var single = _notches.Length == 1;
            if (single) max = min + 1; // Slider needs Minimum < Maximum; disabled below.

            // Order the writes so Minimum < Maximum holds after each one.
            if (min > slider.Maximum)
            {
                slider.Maximum = max;
                slider.Minimum = min;
            }
            else
            {
                slider.Minimum = min;
                slider.Maximum = max;
            }
            slider.Ticks = new Avalonia.Collections.AvaloniaList<double>(_notches);
            slider.IsSnapToTickEnabled = true;
            slider.IsEnabled = !single;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ZoomInButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (AvailableWidth > 0 && _notches.Length > 0)
            {
                Value = SlideGridMetrics.NextNotch(_notches, Value, +1);
                return;
            }
            Value = Math.Min(150d, (double)((decimal)Value + 10m));
        }

        private void ZoomOutButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (AvailableWidth > 0 && _notches.Length > 0)
            {
                Value = SlideGridMetrics.NextNotch(_notches, Value, -1);
                return;
            }
            Value = Math.Max(50d, (double)((decimal)Value - 10m));
        }
    }
}
