using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using HandsLiftedApp.Controls.Converters;
using HandsLiftedApp.Converters;
using HandsLiftedApp.Data.Models.SlideElement;
using ReactiveUI;

namespace HandsLiftedApp.Core.Render.CustomSlide
{
    public partial class CustomSlideRender : UserControl
    {
        public CustomSlideRender()
        {
            InitializeComponent();

            PointerPressed += (sender, args) =>
            {
                if (DataContext is Data.Data.Models.Slides.CustomSlide customSlide)
                    Render(customSlide);
            };

            RegisterDataContext();

            DataContextChanged += (sender, args) => { RegisterDataContext(); };
        }

        private void RegisterDataContext()
        {
            if (DataContext is Data.Data.Models.Slides.CustomSlide customSlide)
            {
                Render(customSlide);

                customSlide.WhenAnyValue(x => x.SlideElements)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe((ObservableCollection<SlideElement> SlideElements) =>
                    {
                        Dispatcher.UIThread.InvokeAsync(() => Render(customSlide));
                    });

                customSlide.SlideElements.CollectionChanged += (sender, args) => Render(customSlide);
            }
        }

        public void Render(Data.Data.Models.Slides.CustomSlide customSlide)
        {
            Root.Children.Clear();
            int i = 0;
            foreach (SlideElement slideElement in customSlide.SlideElements)
            {
                var control = CreateControlForElement(slideElement);
                if (control is not null)
                    Root.Children.Add(control);
            }
        }

        public static Control? CreateControlForElement(SlideElement slideElement)
        {
            if (slideElement is TextElement textElement)
            {
                TextBlock textBlock = new TextBlock()
                {
                    Text = textElement.Text, FontSize = Math.Max(1, textElement.FontSize),
                    Background = Brushes.Blue, Foreground = Brushes.Yellow,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
                };
                Canvas.SetLeft(textBlock, textElement.X);
                Canvas.SetTop(textBlock, textElement.Y);


                textBlock.Bind(TextBlock.TextProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Text),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.FontSizeProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.FontSize),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(Canvas.TopProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Y),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(Canvas.LeftProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.X),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.WidthProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Width),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.HeightProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Height),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.TextAlignmentProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.TextAlignment),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.LineHeightProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.LineHeight),
                    Mode = BindingMode.TwoWay
                });

                textBlock.Bind(TextBlock.ForegroundProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.ForegroundAvaloniaColour),
                    Mode = BindingMode.OneWay,
                    Converter = new XmlColorToBrushConverter()
                });
                
                textBlock.Bind(TextBlock.BackgroundProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.BackgroundAvaloniaColour),
                    Mode = BindingMode.OneWay,
                    Converter = new XmlColorToBrushConverter()
                });
                
                return textBlock;
            }
            return null;
        }

        private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is Data.Data.Models.Slides.CustomSlide customSlide)
                Render(customSlide);
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is Data.Data.Models.Slides.CustomSlide customSlide)
                Render(customSlide);
        }
    }
}