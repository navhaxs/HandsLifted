using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Controls
{
    public partial class SearchBox : UserControl
    {
        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<SearchBox, string>(nameof(SearchText), defaultValue: string.Empty);

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<SearchBox, string>(nameof(Watermark), "Search");

        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public static readonly RoutedEvent<RoutedEventArgs> SearchClickedEvent =
            RoutedEvent.Register<SearchBox, RoutedEventArgs>(nameof(SearchClicked), RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs>? SearchClicked
        {
            add => AddHandler(SearchClickedEvent, value);
            remove => RemoveHandler(SearchClickedEvent, value);
        }

        public SearchBox()
        {
            InitializeComponent();
        }
 
        private void SearchButton_OnClick(object? sender, RoutedEventArgs e)
        {
            TriggerSearch();
        }

        private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;
        }

        private void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TriggerSearch();
            }
        }

        private void TriggerSearch()
        {
            var args = new RoutedEventArgs(SearchClickedEvent, this);
            RaiseEvent(args);
        }
    }
}