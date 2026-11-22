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
using Avalonia.VisualTree;
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
            
            InlineEditor.Committed += OnInlineEditCommitted;
            InlineEditor.ElementChanged += (_, _) => PositionInlineEditor();

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
            // keys typed into the inline text box bubble up here too: "f" must type an f, not fill the view
            if (e.Handled || InlineEditor.IsEditing || e.Source is TextBox) return;

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

        // ---------------- inline text editing ----------------

        /// <summary>True while a text element is being typed into on the canvas.</summary>
        public bool IsEditing => InlineEditor.IsEditing;

        /// <summary>The element being typed into, or null.</summary>
        public TextElement? EditingElement => InlineEditor.Element;

        /// <summary>
        /// Opens the inline text box over <paramref name="el"/> (which must be on the slide shown). Its canvas text is
        /// hidden while editing, and the selection handles are put away so clicks reach the box. With
        /// <paramref name="selectAll"/> the current text is selected so typing replaces it.
        /// </summary>
        public void BeginEdit(TextElement el, bool selectAll = false)
        {
            if (_renderedSlide == null || !_renderedSlide.SlideElements.Contains(el)) return;
            if (ReferenceEquals(InlineEditor.Element, el)) return;

            CommitEdit();

            // The selection adorner lives in the window's adorner layer, above this canvas; its drag thumb covers the
            // element and would take every click meant for the text box. It comes back on commit.
            RemoveSelected();

            HiddenTextElement = el;
            InlineEditor.Begin(el, selectAll);
            PositionInlineEditor();
        }

        /// <summary>Ends the inline edit, writing the text to the element only if it changed (one undo step).</summary>
        public void CommitEdit() => InlineEditor.Commit();

        private void OnInlineEditCommitted(TextElement el, string newText)
        {
            if (ReferenceEquals(HiddenTextElement, el)) HiddenTextElement = null;

            // Ended with Esc (not by clicking somewhere else): the hidden box had the focus, so give it to the canvas
            // so the editor's keys (undo, and the canvas shortcuts) keep working.
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focused == null || ReferenceEquals(focused, InlineEditor)) _zoomBorder?.Focus();

            var onSlide = _renderedSlide != null && _renderedSlide.SlideElements.Contains(el);
            if (!onSlide) return;

            if (el.Text != newText) el.Text = newText;

            // Put the handles back on the element, unless the click that ended the edit already selected something
            // else (that click's own handler runs after the focus change that commits, and wins either way).
            if (_selected == null) SelectElement(el);
        }

        /// <summary>Ends an edit whose element is no longer on the slide shown, without writing to it.</summary>
        private void CancelEditIfElementGone()
        {
            var el = InlineEditor.Element;
            if (el == null) return;
            if (_renderedSlide != null && _renderedSlide.SlideElements.Contains(el)) return;

            InlineEditor.Cancel();
            if (ReferenceEquals(HiddenTextElement, el)) HiddenTextElement = null;
        }

        private void PositionInlineEditor()
        {
            if (InlineEditor.Element is not { } el) return;
            Canvas.SetLeft(InlineEditor, el.X);
            Canvas.SetTop(InlineEditor, el.Y);
        }

        // Opens the edit after the double-click's own pointer handling (focus on press, pointer capture) is done, so
        // that does not immediately take the focus back from the box.
        private void BeginEditAfterDoubleClick(SlideElement? element)
        {
            if (element is not TextElement text) return;
            Dispatcher.UIThread.Post(() => BeginEdit(text));
        }

        private void RegisterDataContext()
        {
            // The text being typed belongs to the slide shown until now.
            CommitEdit();
            CancelEditIfElementGone();

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
                // Skip the current list: it was just rendered above (rendering it again later would also rebuild the
                // proxies under an open inline edit for nothing).
                _slideElementsSubscription = customSlide.WhenAnyValue(x => x.SlideElements)
                    .Skip(1)
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

            // e.g. the element being typed into was deleted, or an undo replaced it
            CancelEditIfElementGone();

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
                    if (e.ClickCount == 2 && e.GetCurrentPoint(proxy).Properties.IsLeftButtonPressed)
                        BeginEditAfterDoubleClick(slideElement);
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

            // Once an element is selected its handles' drag thumb covers it, so the second click of a double-click
            // lands on the adorner, not the proxy (the adorner layer is not under this control, so it is caught here).
            // The thumb marks the press handled, hence handledEventsToo.
            _selected.AddHandler(InputElement.PointerPressedEvent, OnAdornerPointerPressed, RoutingStrategies.Bubble,
                handledEventsToo: true);

            ((ISetLogicalParent)_selected).SetParent(canvas);
            layer.Children.Add(_selected);
            
            // make sure someone is listening to event
            if (OnUpdateSelectedElement == null) return;

            OnUpdateSelectedElementEventArgs args = new OnUpdateSelectedElementEventArgs(control.DataContext as SlideElement);
            OnUpdateSelectedElement(this, args);
        }
        
        private void OnAdornerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not SelectedAdorner adorner || e.ClickCount != 2) return;
            if (!e.GetCurrentPoint(adorner).Properties.IsLeftButtonPressed) return;
            // only the body (drag thumb), not a resize handle
            var thumb = (e.Source as Visual)?.FindAncestorOfType<Thumb>(includeSelf: true);
            if (thumb?.Name != "PART_Drag") return;
            BeginEditAfterDoubleClick(adorner.Control?.DataContext as SlideElement);
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