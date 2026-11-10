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
                if (DataContext is global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
                    Render(customSlide);
            };

            RegisterDataContext();

            DataContextChanged += (sender, args) => { RegisterDataContext(); };
        }

        private global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide? _watchedSlide;

        private void OnWatchedSlideChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide.BackgroundGraphicFilePath)
                && sender is global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide slide)
                LoadBackgroundAsync(slide);
        }

        private void RegisterDataContext()
        {
            // follow the background image path of the slide we are showing (load it again when it changes)
            if (_watchedSlide != null) _watchedSlide.PropertyChanged -= OnWatchedSlideChanged;
            _watchedSlide = DataContext as global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;
            if (_watchedSlide != null) _watchedSlide.PropertyChanged += OnWatchedSlideChanged;

            if (DataContext is global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
            {
                Render(customSlide);
                LoadBackgroundAsync(customSlide);

                customSlide.WhenAnyValue(x => x.SlideElements)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe((ObservableCollection<SlideElement> SlideElements) =>
                    {
                        Dispatcher.UIThread.InvokeAsync(() => Render(customSlide));
                    });

                customSlide.SlideElements.CollectionChanged += (sender, args) => Render(customSlide);
            }
        }

        private void LoadBackgroundAsync(global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
        {
            ImagePathLoader.Load(
                customSlide.BackgroundGraphicFilePath,
                () => (DataContext as global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide)?.BackgroundGraphicFilePath,
                bitmap => BackgroundBorder.Background =
                    bitmap == null ? null : new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill });
        }

        public void Render(global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
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
                Image image = new Image()
                {
                    Source = null,
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

                // Load now, and again whenever the path changes (adding an image first and picking its file after
                // is the normal flow). Only listen while the control is on screen: Render() builds a fresh control
                // for every element on each change, and a permanent subscription would keep every old one alive.
                void ReloadImage() => ImagePathLoader.Load(imageElement.FilePath, () => imageElement.FilePath,
                    bitmap => image.Source = bitmap);

                void OnElementChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
                {
                    if (e.PropertyName == nameof(ImageElement.FilePath)) ReloadImage();
                }

                image.AttachedToVisualTree += (_, _) =>
                {
                    imageElement.PropertyChanged -= OnElementChanged;
                    imageElement.PropertyChanged += OnElementChanged;
                    ReloadImage(); // the path may have changed while the control was off screen
                };
                image.DetachedFromVisualTree += (_, _) => imageElement.PropertyChanged -= OnElementChanged;

                ReloadImage();

                return image;
            }

            if (slideElement is TextElement textElement)
            {
                Border textBlockContainer = new Border()
                {
                    DataContext = textElement,
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
            if (DataContext is global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
                Render(customSlide);
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is global::HandsLiftedApp.Data.Data.Models.Slides.CustomSlide customSlide)
                Render(customSlide);
        }
    }
}