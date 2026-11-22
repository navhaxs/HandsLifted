using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.ViewModels.SlideElementEditor;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using PerspectiveDemo;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class CustomSlideDesigner : UserControl
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
        
        public delegate void OnDeselectElementHandler(object sender, OnUpdateSelectedElementEventArgs e);
        public event OnDeselectElementHandler OnDeselectElement;
        
        public CustomSlideDesigner()
        {
            InitializeComponent();

            var canvas = this.FindControl<Canvas>("Root");

            canvas.PointerPressed += (sender, args) =>
            {
                RemoveSelected();
                // TODO could pass last selected element to OnDeselectElement args
                OnDeselectElement?.Invoke(this, new OnUpdateSelectedElementEventArgs(null));
            };
            
            RegisterDataContext();

            DataContextChanged += (sender, args) => { RegisterDataContext(); };

            _zoomBorder = this.Find<ZoomBorder>("ZoomBorder");
            if (_zoomBorder != null)
            {
                _zoomBorder.KeyDown += ZoomBorder_KeyDown;
                _zoomBorder.ZoomChanged += ZoomBorder_ZoomChanged;
                // Fit the slide once the zoom border first has a size (this used to be a fixed 1 s sleep on the UI thread).
                _zoomBorder.SizeChanged += ZoomBorder_FirstSizeChanged;
            }
        }

        private bool _initialFitDone;

        private void ZoomBorder_FirstSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_initialFitDone || _zoomBorder == null || e.NewSize.Width <= 0 || e.NewSize.Height <= 0) return;
            _initialFitDone = true;
            _zoomBorder.SizeChanged -= ZoomBorder_FirstSizeChanged;

            // after the current layout pass, so the zoom border's bounds and its child are both arranged
            Dispatcher.UIThread.Post(() =>
            {
                _zoomBorder.Uniform();
                _zoomBorder.ZoomOut();
            }, DispatcherPriority.Background);
        }

        /// <summary>The canvas zoom as a percentage (100 = the slide at its own pixel size).</summary>
        public double ZoomPercent => (_zoomBorder?.ZoomX ?? 1.0) * 100.0;

        /// <summary>Raised whenever the canvas zoom changes (buttons, mouse wheel, fit).</summary>
        public event EventHandler? ZoomChanged;

        public void ZoomIn() => _zoomBorder?.ZoomIn();

        public void ZoomOut() => _zoomBorder?.ZoomOut();

        /// <summary>Scales the slide to fit the visible canvas area.</summary>
        public void FitToView() => _zoomBorder?.Uniform();

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
                
            // Update adorner clipping bounds when zoom changes
            UpdateAdornerClippingBounds();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A text element whose glyphs the canvas does not draw (its fill still is), e.g. while it is edited inline.
        /// </summary>
        public TextElement? HiddenTextElement
        {
            get => SlideCanvas.HiddenTextElement;
            set => SlideCanvas.HiddenTextElement = value;
        }

        // What RegisterDataContext subscribed to, so a DataContext change does not leave the old slide's handlers behind.
        private IDisposable? _slideElementsSubscription;
        private ObservableCollection<SlideElement>? _watchedElements;
        private CustomSlide? _renderedSlide;

        private void RegisterDataContext()
        {
            _slideElementsSubscription?.Dispose();
            _slideElementsSubscription = null;
            WatchElements(null);

            var customSlide = DataContext as CustomSlide;
            _renderedSlide = customSlide;
            SlideCanvas.Slide = customSlide;

            if (customSlide != null)
            {
                WatchElements(customSlide.SlideElements);
                Render(customSlide);

                // The collection itself can be swapped (e.g. undo/redo or code edits replacing the list).
                _slideElementsSubscription = customSlide.WhenAnyValue(x => x.SlideElements)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe(elements =>
                    {
                        if (!ReferenceEquals(_renderedSlide, customSlide)) return;
                        WatchElements(elements);
                        Render(customSlide);
                    });
            }
            else
            {
                RemoveSelected();
                Root.Children.Clear();
            }
        }

        private void WatchElements(ObservableCollection<SlideElement>? elements)
        {
            if (_watchedElements != null) _watchedElements.CollectionChanged -= OnWatchedElementsChanged;
            _watchedElements = elements;
            if (_watchedElements != null) _watchedElements.CollectionChanged += OnWatchedElementsChanged;
        }

        private void OnWatchedElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_renderedSlide != null) Render(_renderedSlide);
        }

        /// <summary>
        /// Rebuilds the transparent hit-test proxies in <c>Root</c>, one per element (the slide itself is drawn by
        /// <see cref="CustomSlideEditorCanvas"/> underneath). Each proxy is bound two-way to its element's rect, so the
        /// selection adorner moving/resizing a proxy edits the element, and edits elsewhere move the proxy.
        /// </summary>
        public void Render(CustomSlide customSlide)
        {
            // The proxies are rebuilt, so the selection adorner would be left on a detached one (e.g. after Bring
            // forward or Duplicate): drop it and, if its element is still on the slide, put it on the new proxy.
            var selectedElement = _selected?.Control?.DataContext as SlideElement;
            RemoveSelected();

            Root.Children.Clear();
            foreach (SlideElement slideElement in customSlide.SlideElements.Reverse())
            {
                var proxy = CreateProxyForElement(slideElement);

                proxy.PointerPressed += (s, e) =>
                {
                    RemoveSelected();
                    AddSelected(proxy, Root);
                    _zoomBorder?.Focus();
                    e.Handled = true;
                };

                Root.Children.Add(proxy);
            }

            if (selectedElement != null && customSlide.SlideElements.Contains(selectedElement))
                SelectElement(selectedElement);
        }

        /// <summary>Removes the selection adorner without raising <see cref="OnDeselectElement"/>.</summary>
        public void ClearSelection() => RemoveSelected();

        private static Border CreateProxyForElement(SlideElement slideElement)
        {
            var proxy = new Border
            {
                Background = Brushes.Transparent, // hit-testable but invisible
                DataContext = slideElement,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = slideElement.Width,
                Height = slideElement.Height,
            };
            Canvas.SetLeft(proxy, slideElement.X);
            Canvas.SetTop(proxy, slideElement.Y);

            proxy.Bind(Canvas.LeftProperty, new Binding
            {
                Source = slideElement, Path = nameof(SlideElement.X), Mode = BindingMode.TwoWay
            });
            proxy.Bind(Canvas.TopProperty, new Binding
            {
                Source = slideElement, Path = nameof(SlideElement.Y), Mode = BindingMode.TwoWay
            });
            proxy.Bind(Layoutable.WidthProperty, new Binding
            {
                Source = slideElement, Path = nameof(SlideElement.Width), Mode = BindingMode.TwoWay
            });
            proxy.Bind(Layoutable.HeightProperty, new Binding
            {
                Source = slideElement, Path = nameof(SlideElement.Height), Mode = BindingMode.TwoWay
            });

            return proxy;
        }

        public void SelectElement(SlideElement slideElement)
        {
            if (_selected?.Control?.DataContext == slideElement)
            {
                return;
            }
            
            var first = Root.Children.FirstOrDefault((control) => control.DataContext == slideElement);

            RemoveSelected();
            if (first != null)
            {
                AddSelected(first, Root);
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
                ZoomBorder = _zoomBorder,
                // ClippingBounds = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height)
            };
            
            // Calculate clipping bounds in the adorner's coordinate system
            UpdateAdornerClippingBounds();

            ((ISetLogicalParent)_selected).SetParent(canvas);
            layer.Children.Add(_selected);
            
            // make sure someone is listening to event
            if (OnUpdateSelectedElement == null) return;

            OnUpdateSelectedElementEventArgs args = new OnUpdateSelectedElementEventArgs(control.DataContext as SlideElement);
            OnUpdateSelectedElement(this, args);
        }
        
        private void UpdateAdornerClippingBounds()
        {
            if (_selected == null) return;

            // Get the canvas that the adorner is attached to
            var canvas = Root as Canvas;
            if (canvas == null) return;

            // Get the bounds of the VisualSlideEditor
            var editorBounds = this.Bounds;
    
            // Transform the VisualSlideEditor bounds to the canvas coordinate system
            var topLeftInCanvas = this.TranslatePoint(new Point(0, 0), canvas);
            var bottomRightInCanvas = this.TranslatePoint(new Point(editorBounds.Width, editorBounds.Height), canvas);
    
            if (topLeftInCanvas.HasValue && bottomRightInCanvas.HasValue)
            {
                var clippingBounds = new Rect(
                    topLeftInCanvas.Value.X,
                    topLeftInCanvas.Value.Y,
                    bottomRightInCanvas.Value.X - topLeftInCanvas.Value.X,
                    bottomRightInCanvas.Value.Y - topLeftInCanvas.Value.Y
                );
        
                _selected.ClippingBounds = clippingBounds;
            }
        }
        
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
    
            // Update clipping bounds when the editor size changes
            UpdateAdornerClippingBounds();
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
    }
}