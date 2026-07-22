using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

        public ThumbnailSizeSlider()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ZoomInButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Value = Math.Min(150d, (double)((decimal)Value + 10m));
        }

        private void ZoomOutButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Value = Math.Max(50d, (double)((decimal)Value - 10m));
        }
    }
}
