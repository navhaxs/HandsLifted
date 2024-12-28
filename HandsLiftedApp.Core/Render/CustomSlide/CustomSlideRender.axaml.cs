using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
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
                    .Subscribe((ObservableCollection<SlideElement> SlideElements) => { Dispatcher.UIThread.InvokeAsync(() => Render(customSlide)); });
                
                customSlide.SlideElements.CollectionChanged += (sender, args) => Render(customSlide);
            }
        }

        public void Render(Data.Data.Models.Slides.CustomSlide customSlide)
        {
            Root.Children.Clear();
            int i = 0;
            foreach (SlideElement slideElement in customSlide.SlideElements)
            {
                if (slideElement is TextElement textElement)
                {
                    TextBlock textBlock = new TextBlock()
                    {
                        Text = textElement.Text, FontSize = Math.Max(1, textElement.FontSize),
                        Background = Brushes.Blue, Foreground = Brushes.Yellow, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
                    };
                    Canvas.SetLeft(textBlock, textElement.X);
                    Canvas.SetTop(textBlock, textElement.Y);
                    Root.Children.Add(textBlock);
                }
            }
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