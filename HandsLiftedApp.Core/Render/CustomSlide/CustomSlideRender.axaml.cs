using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Controls.Converters;
using HandsLiftedApp.Converters;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Utils;
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
            foreach (SlideElement slideElement in customSlide.SlideElements.Reverse())
            {
                var control = CreateControlForElement(slideElement);
                if (control is not null)
                    Root.Children.Add(control);
            }
        }

        public static Control? CreateControlForElement(SlideElement slideElement)
        {
            if (slideElement is ImageElement imageElement)
            {
                Bitmap? imageData = null;
                try
                {
                    if (!string.IsNullOrEmpty(imageElement.FilePath))
                    {
                        imageData = BitmapLoader.LoadBitmap(imageElement.FilePath);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error loading image: {e.Message}");
                }

                Image image = new Image()
                {
                    Source = imageData,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Stretch = Stretch.Uniform,
                    DataContext = slideElement,
                    Width = imageElement.Width,
                    Height = imageElement.Height,
                };
                Canvas.SetLeft(image, imageElement.X);
                Canvas.SetTop(image, imageElement.Y);

                image.Bind(Canvas.TopProperty, new Binding
                {
                    Source = imageElement,
                    Path = nameof(imageElement.Y),
                    Mode = BindingMode.TwoWay
                });

                image.Bind(Canvas.LeftProperty, new Binding
                {
                    Source = imageElement,
                    Path = nameof(imageElement.X),
                    Mode = BindingMode.TwoWay
                });

                image.Bind(WidthProperty, new Binding
                {
                    Source = imageElement,
                    Path = nameof(imageElement.Width),
                    Mode = BindingMode.TwoWay
                });

                image.Bind(HeightProperty, new Binding
                {
                    Source = imageElement,
                    Path = nameof(imageElement.Height),
                    Mode = BindingMode.TwoWay
                });

                image.Bind(Image.SourceProperty, new Binding
                {
                    Source = imageElement,
                    Path = nameof(imageElement.FilePath),
                    Converter = BitmapAssetValueConverter.Instance,
                });

                return image;
            }

            if (slideElement is TextElement textElement)
            {
                Border textBlockContainer = new Border()
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                Canvas.SetLeft(textBlockContainer, textElement.X);
                Canvas.SetTop(textBlockContainer, textElement.Y);
                textBlockContainer.Bind(Canvas.TopProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Y),
                    Mode = BindingMode.TwoWay
                });

                textBlockContainer.Bind(Canvas.LeftProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.X),
                    Mode = BindingMode.TwoWay
                });

                textBlockContainer.Bind(Layoutable.WidthProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Width),
                    Mode = BindingMode.TwoWay
                });

                textBlockContainer.Bind(Layoutable.HeightProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.Height),
                    Mode = BindingMode.TwoWay
                });
                
                textBlockContainer.Bind(TextBlock.BackgroundProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.BackgroundAvaloniaColour),
                    Mode = BindingMode.OneWay,
                    Converter = new XmlColorToBrushConverter()
                });

                TextBlock textBlock = new TextBlock()
                {
                    Text = textElement.Text, FontSize = Math.Max(1, textElement.FontSize),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Yellow,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = textElement.VerticalAlignment,
                    TextWrapping = TextWrapping.Wrap,
                    DataContext = slideElement
                };
                textBlockContainer.Child = textBlock;

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
                
                textBlock.Bind(Layoutable.VerticalAlignmentProperty, new Binding
                {
                    Source = textElement,
                    Path = nameof(textElement.VerticalAlignment),
                    Mode = BindingMode.OneWay,
                });

                return textBlockContainer;
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