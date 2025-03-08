using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Converters;
using Avalonia.Threading;
using HandsLiftedApp.Controls.Converters;
using HandsLiftedApp.Core.Render.CustomSlide;
using HandsLiftedApp.Core.ViewModels.SlideElementEditor;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using PerspectiveDemo;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class VisualSlideEditor : UserControl
    {
        private readonly ZoomBorder? _zoomBorder;
        
        public delegate void StatusUpdateHandler(object sender, OnUpdateSelectedElementEventArgs e);
        public event StatusUpdateHandler OnUpdateSelectedElement;

        public class OnUpdateSelectedElementEventArgs : EventArgs
        {
            public SlideElement? SelectedElement { get; private set; }
        
            public OnUpdateSelectedElementEventArgs(SlideElement selectedElement)
            {
                SelectedElement = selectedElement;
            }
        }
        
        public VisualSlideEditor()
        {
            InitializeComponent();

            var canvas = this.FindControl<Canvas>("Root");

            canvas.PointerPressed += (sender, args) =>
            {
                RemoveSelected();
            };

            //
            // TestWindow1 tw1 = new TestWindow1();
            // tw1.Show();

            RegisterDataContext();

            DataContextChanged += (sender, args) => { RegisterDataContext(); };

            _zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
            if (_zoomBorder != null)
            {
                _zoomBorder.KeyDown += ZoomBorder_KeyDown;
                _zoomBorder.ZoomChanged += ZoomBorder_ZoomChanged;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Thread.Sleep(1_000);
                    _zoomBorder.Uniform();
                    _zoomBorder.ZoomOut();
                });
            }
        }

        private void ZoomBorder_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F:
                    _zoomBorder?.Fill();
                    break;
                case Key.U:
                    _zoomBorder?.Uniform();
                    break;
                case Key.R:
                    _zoomBorder?.ResetMatrix();
                    break;
                case Key.T:
                    _zoomBorder?.ToggleStretchMode();
                    _zoomBorder?.AutoFit();
                    break;
            }
        }

        private void ZoomBorder_ZoomChanged(object sender, ZoomChangedEventArgs e)
        {
            Debug.WriteLine($"[ZoomChanged] {e.ZoomX} {e.ZoomY} {e.OffsetX} {e.OffsetY}");
        }

        private void RegisterDataContext()
        {
            if (DataContext is CustomSlide customSlide)
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

        public void Render(CustomSlide customSlide)
        {
            Root.Bind(Panel.BackgroundProperty, new Binding
            {
                Source = customSlide,
                Path = nameof(customSlide.BackgroundAvaloniaColour),
                Mode = BindingMode.OneWay,
                Converter = new ColorToBrushConverter()
            });

            Root.Children.Clear();
            int i = 0;
            foreach (SlideElement slideElement in customSlide.SlideElements)
            {
                var control = CustomSlideRender.CreateControlForElement(slideElement);
                if (control is null)
                    continue;
                    
                control.PointerEntered += (s, e) =>
                {
                    if (_selected is null)
                    {
                        AddSelected(control, Root);
                    }
                };

                Root.Children.Add(control);
            }
        }

        private SelectedAdorner? _selected;

        private void AddSelected(Control control, Canvas canvas)
        {
            var layer = AdornerLayer.GetAdornerLayer(canvas);
            if (layer is null)
            {
                return;
            }

            _selected = new SelectedAdorner
            {
                [AdornerLayer.AdornedElementProperty] = canvas,
                IsHitTestVisible = true,
                ClipToBounds = false,
                Control = control,
                ZoomBorder = _zoomBorder
            };

            ((ISetLogicalParent)_selected).SetParent(canvas);
            layer.Children.Add(_selected);
            
            // ake sure someone is listening to event
            if (OnUpdateSelectedElement == null) return;

            OnUpdateSelectedElementEventArgs args = new OnUpdateSelectedElementEventArgs(control.DataContext as SlideElement);
            OnUpdateSelectedElement(this, args);
        }

        private void RemoveSelected()
        {
            if (_selected == null)
            {
                return;
            }

            var canvas = Root as Canvas;
            if (canvas != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(canvas);
                if (layer != null)
                {
                    layer.Children.Remove(_selected);
                }

                ((ISetLogicalParent)_selected).SetParent(null);
            }

            _selected = null;
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SlideEditorViewModel vm)
            {
                vm.Slides.Add(new CustomSlide());
            }
        }

        private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SlideEditorViewModel vm)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is CustomSlide targetSlide)
                    {
                        vm.Slides.Remove(targetSlide);
                    }
                }
            }
        }

        private void ButtonResetZoom_OnClick(object? sender, RoutedEventArgs e)
        {
            _zoomBorder.Uniform();
        }

        private void ButtonZoomIn_OnClick(object? sender, RoutedEventArgs e)
        {
            _zoomBorder.ZoomIn();
        }
        
        private void ButtonZoomOut_OnClick(object? sender, RoutedEventArgs e)
        {
            _zoomBorder.ZoomOut();
        }

        private void RangeBase_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            // TODO actually want the centre point X, Y
            _zoomBorder?.Zoom(e.NewValue, _zoomBorder.OffsetX, _zoomBorder.OffsetY);
        }
    }
}