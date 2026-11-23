using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Utils;
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

            // dropped picture files: the scroll viewer covers the grey area, the slide and the proxies (AllowDrop is
            // inherited); the selection adorner, outside this tree, gets the same in AddSelected
            EnableImageDrop(MainScrollViewer);
        }

        /// <summary>Makes <paramref name="target"/> accept dropped picture files (DragOver/Drop handled here).</summary>
        private void EnableImageDrop(Interactive target)
        {
            DragDrop.SetAllowDrop(target, true);
            target.AddHandler(DragDrop.DragOverEvent, Canvas_DragOver);
            target.AddHandler(DragDrop.DropEvent, Canvas_Drop);
        }

        // ---------------- dropped / pasted picture files ----------------

        /// <summary>Raised when picture files are dropped on the canvas: their local paths and the slide point.</summary>
        public event Action<IReadOnlyList<string>, Point>? ImageFilesDropped;

        /// <summary>Raised by Ctrl+V on the canvas (not while typing in the inline box); the editor reads the clipboard.</summary>
        public event EventHandler? PasteRequested;

        /// <summary>The paths among <paramref name="paths"/> that are pictures by extension (any case), in order.</summary>
        internal static IReadOnlyList<string> ImageFilePaths(IEnumerable<string?>? paths) =>
            paths?.Where(PortableAssetCopier.IsImageFile).Select(p => p!).ToList() ?? new List<string>();

        /// <summary>
        /// DragOver's answer: refused when the drag carries no files at all; accepted (Copy) when it does, unless its
        /// file list is readable and holds no picture. A list that can't be read during the drag (null, empty, no
        /// local paths) never refuses it: Drop filters the files again and does nothing without a picture.
        /// </summary>
        internal static DragDropEffects DropEffectFor(bool containsFiles, IReadOnlyList<string?>? readablePaths)
        {
            if (!containsFiles) return DragDropEffects.None;
            var readable = readablePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (readable == null || readable.Count == 0) return DragDropEffects.Copy;
            return ImageFilePaths(readable).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }

        /// <summary>The selection adorner (in the window's adorner layer), or null; for tests.</summary>
        internal Control? SelectionAdorner => _selected;

        /// <summary>
        /// Reports the picture files among <paramref name="paths"/> dropped at <paramref name="slidePoint"/> (slide
        /// coordinates). Returns whether there were any.
        /// </summary>
        internal bool OnFilesDropped(IEnumerable<string?>? paths, Point slidePoint)
        {
            var images = ImageFilePaths(paths);
            if (images.Count == 0) return false;
            ImageFilesDropped?.Invoke(images, slidePoint);
            return true;
        }

        /// <summary>The dragged files' local paths, or null when the list can't be read (never logs: DragOver calls it per move).</summary>
        private static IReadOnlyList<string?>? TryLocalPaths(IDataTransfer? data)
        {
            try
            {
                return data?.TryGetFiles()?.Select(item => item.TryGetLocalPath()).ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Canvas_DragOver(object? sender, DragEventArgs e)
        {
            var containsFiles = e.DataTransfer?.Contains(DataFormat.File) == true;
            e.DragEffects &= DropEffectFor(containsFiles, containsFiles ? TryLocalPaths(e.DataTransfer) : null);
            e.Handled = true;
        }

        private void Canvas_Drop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            var paths = TryLocalPaths(e.DataTransfer);
            if (paths == null) Serilog.Log.Warning("Custom slide designer: could not read the dropped files");
            // Root's coordinates are slide coordinates, whatever the zoom and pan (and from the adorner layer too)
            if (OnFilesDropped(paths, e.GetPosition(Root)))
            {
                e.DragEffects &= DragDropEffects.Copy;
                // so the canvas shortcuts (Delete, arrows, Ctrl+Z) work on the dropped pictures straight away
                _zoomBorder?.Focus();
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
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

            if (HandleKey(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                return;
            }

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

        /// <summary>
        /// The canvas keyboard shortcuts: Delete/Back remove the selected element, arrows nudge it 1 px (Shift: 10),
        /// Ctrl+D duplicates it and selects the copy, Esc deselects, Enter/F2 edit a selected text element, Ctrl+V raises
        /// <see cref="PasteRequested"/> (with or without a selection). Returns whether the key was handled; always
        /// false while typing in the inline box (the text box pastes text itself), and false with nothing selected
        /// for every key except Ctrl+V.
        /// </summary>
        internal bool HandleKey(Key key, KeyModifiers modifiers)
        {
            if (InlineEditor.IsEditing) return false;
            if (DataContext is not CustomSlide slide) return false;

            if (key == Key.V && modifiers == KeyModifiers.Control)
            {
                PasteRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            var element = _selectedElement;
            if (element == null || !slide.SlideElements.Contains(element)) return false;

            var plain = modifiers == KeyModifiers.None;
            var step = modifiers == KeyModifiers.Shift ? 10 : 1;
            var stepping = plain || modifiers == KeyModifiers.Shift;

            switch (key)
            {
                case Key.Delete or Key.Back when plain:
                    SlideElementCommands.Delete(slide, element);
                    RemoveSelected();
                    OnDeselectElement?.Invoke(this, new OnUpdateSelectedElementEventArgs(null));
                    return true;
                case Key.Left when stepping:
                    return SlideElementCommands.Nudge(element, -step, 0);
                case Key.Right when stepping:
                    return SlideElementCommands.Nudge(element, step, 0);
                case Key.Up when stepping:
                    return SlideElementCommands.Nudge(element, 0, -step);
                case Key.Down when stepping:
                    return SlideElementCommands.Nudge(element, 0, step);
                case Key.D when modifiers == KeyModifiers.Control:
                    var copy = SlideElementCommands.Duplicate(slide, element);
                    if (copy == null) return false;
                    SelectElement(copy);
                    return true;
                case Key.Escape when plain:
                    RemoveSelected();
                    OnDeselectElement?.Invoke(this, new OnUpdateSelectedElementEventArgs(null));
                    return true;
                case Key.Enter or Key.F2 when plain && element is TextElement text:
                    BeginEdit(text);
                    return true;
                default:
                    return false;
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
        /// hidden while editing. The element stays selected, so its outline and resize handles stay up while typing;
        /// only the adorner's move area is made click-through so clicks reach the box. With
        /// <paramref name="selectAll"/> the current text is selected so typing replaces it.
        /// </summary>
        public void BeginEdit(TextElement el, bool selectAll = false)
        {
            if (_renderedSlide == null || !_renderedSlide.SlideElements.Contains(el)) return;
            if (ReferenceEquals(InlineEditor.Element, el)) return;

            CommitEdit();

            HiddenTextElement = el;
            InlineEditor.Begin(el, selectAll);
            PositionInlineEditor();

            // The selection adorner lives in the window's adorner layer, above this canvas; its move area covers the
            // element and would take every click meant for the text box (and the drag that selects text), so that
            // part stops taking the pointer while editing. A new adorner is created already click-through
            // (AddSelected asks SelectionBodyHitTestableFor), an existing one is switched.
            if (!ReferenceEquals(_selectedElement, el)) SelectElement(el);
            else if (_selected != null) _selected.BodyHitTestable = false;
        }

        /// <summary>The element the selection outline and handles are on, or null.</summary>
        internal SlideElement? SelectedElement => _selectedElement;

        /// <summary>
        /// Whether the selection's move area should take the pointer for <paramref name="element"/>: not while that
        /// element's text is being typed into (the text box underneath needs the clicks).
        /// </summary>
        internal bool SelectionBodyHitTestableFor(SlideElement? element) =>
            !(InlineEditor.IsEditing && ReferenceEquals(InlineEditor.Element, element));

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

            // The element is still selected (its adorner stayed up during the edit): let its move area take the pointer
            // again. If nothing is selected any more (the adorner was rebuilt away, or the click that ended the edit
            // deselected) put the handles back on the element, unless that click selected something else (its own
            // handler runs after the focus change that commits, and wins either way).
            if (_selectedElement == null) SelectElement(el);
            else if (ReferenceEquals(_selectedElement, el) && _selected != null) _selected.BodyHitTestable = true;
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

        // The Visual/Code tab switch detaches this designer and attaches it again. While detached it holds no
        // subscriptions on the slide (a hidden designer would otherwise re-render on every change, and the slide would
        // keep it alive); on re-attach it subscribes and renders again. The first attach needs nothing: the
        // constructor already registered.
        private bool _detached;

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _detached = true;
            _slideElementsSubscription?.Dispose();
            _slideElementsSubscription = null;
            WatchElements(null);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (!_detached) return;
            _detached = false;
            RegisterDataContext(); // unsubscribes first, then subscribes and renders once
        }

        private void RegisterDataContext()
        {
            // changed while detached: the re-attach registers it (nothing to watch or draw in the meantime)
            if (_detached)
            {
                CommitEdit();
                CancelEditIfElementGone();
                _renderedSlide = DataContext as CustomSlide;
                SlideCanvas.Slide = _renderedSlide;
                return;
            }

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
            var selectedElement = _selectedElement;
            RemoveSelected();

            // e.g. the element being typed into was deleted, or an undo replaced it
            CancelEditIfElementGone();

            Root.Children.Clear();
            foreach (SlideElement slideElement in customSlide.SlideElements.Reverse())
            {
                var proxy = CreateProxyForElement(slideElement);
                if (ElementContextMenuFactory != null) proxy.ContextMenu = ElementContextMenuFactory(slideElement);

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
            if (_selectedElement == slideElement)
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

        /// <summary>
        /// Makes the right-click menu for an element (the editor's shared one, the same as the Layers list rows). Each
        /// call must return a new menu. Set once by the editor before slides are rendered.
        /// </summary>
        internal Func<SlideElement, ContextMenu>? ElementContextMenuFactory { get; set; }

        private bool _aspectLocked;

        /// <summary>Every handle resize keeps the selected element's aspect ratio (the editor's lock toggle).</summary>
        internal bool AspectLocked
        {
            get => _aspectLocked;
            set
            {
                _aspectLocked = value;
                if (_selected != null) _selected.LockAspect = value;
            }
        }

        // The selected element, tracked apart from the adorner so the keyboard shortcuts also work (and can be tested)
        // where there is no adorner layer.
        private SlideElement? _selectedElement;

        /// <summary>
        /// Finds the adorner layer the selection adorner goes in (the window's). Injectable because a test host without
        /// a window has none.
        /// </summary>
        internal Func<Visual, AdornerLayer?> AdornerLayerLookup { get; set; } = AdornerLayer.GetAdornerLayer;

        private void AddSelected(Control control, Canvas canvas)
        {
            var layer = AdornerLayerLookup(canvas);
            if (layer is null)
            {
                _selectedElement = control.DataContext as SlideElement;
                OnUpdateSelectedElement?.Invoke(this, new OnUpdateSelectedElementEventArgs(_selectedElement));
                return;
            }

            _selectedElement = control.DataContext as SlideElement;
            _selected = new SelectedAdorner
            {
                [AdornerLayer.AdornedElementProperty] = canvas,
                IsHitTestVisible = true,
                ClipToBounds = false,
                Control = control,
                ZoomBorder = _zoomBorder,
                SlideSize = DataContext is CustomSlide sizedSlide
                    ? new Size(sizedSlide.SlideWidth, sizedSlide.SlideHeight)
                    : new Size(1920, 1080),
                SnapTargetsProvider = GetSnapTargets,
                LockAspect = _aspectLocked,
                // a rebuilt adorner (Render, undo) for the element being typed into must not cover the text box
                BodyHitTestable = SelectionBodyHitTestableFor(control.DataContext as SlideElement),
                // ClippingBounds = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height)
            };
            
            // Calculate clipping bounds in the adorner's coordinate system
            UpdateAdornerClippingBounds();

            // Once an element is selected its handles' drag thumb covers it, so the second click of a double-click
            // lands on the adorner, not the proxy (the adorner layer is not under this control, so it is caught here).
            // The thumb marks the press handled, hence handledEventsToo.
            _selected.AddHandler(InputElement.PointerPressedEvent, OnAdornerPointerPressed, RoutingStrategies.Bubble,
                handledEventsToo: true);

            // The adorner covers the selected element, so a right-click there lands on it, not on the proxy.
            if (_selectedElement != null && ElementContextMenuFactory != null)
                _selected.ContextMenu = ElementContextMenuFactory(_selectedElement);

            // The thumb covers the selected element's whole rect, including the part another element is drawn over, so
            // a click there never reaches that element's proxy. Look before the thumb takes the press (tunnel).
            _selected.AddHandler(InputElement.PointerPressedEvent, OnAdornerPointerPressedPreview, RoutingStrategies.Tunnel);

            // Its drag thumb covers the element (a picture fitted to the slide covers the whole slide) and drag events
            // bubble from it through the adorner layer, never through MainScrollViewer: take drops here too.
            EnableImageDrop(_selected);

            ((ISetLogicalParent)_selected).SetParent(canvas);
            layer.Children.Add(_selected);
            
            // make sure someone is listening to event
            if (OnUpdateSelectedElement == null) return;

            OnUpdateSelectedElementEventArgs args = new OnUpdateSelectedElementEventArgs(control.DataContext as SlideElement);
            OnUpdateSelectedElement(this, args);
        }
        
        // Rects of every element except the selected one, read fresh on each drag move.
        private System.Collections.Generic.IReadOnlyList<Rect> GetSnapTargets()
        {
            if (DataContext is not CustomSlide slide) return Array.Empty<Rect>();
            return slide.SlideElements
                .Where(e => e != _selectedElement)
                .Select(e => new Rect(e.X, e.Y, e.Width, e.Height))
                .ToList();
        }

        /// <summary>
        /// A press on the selected element's move area that lands on an element drawn in front of it (list index 0 is
        /// frontmost) selects that element instead, as if the selection outline were not there.
        /// </summary>
        private void OnAdornerPointerPressedPreview(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not SelectedAdorner adorner || _selectedElement == null) return;
            if (DataContext is not CustomSlide slide) return;
            var buttons = e.GetCurrentPoint(adorner).Properties;
            if (!buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed) return; // right: its context menu
            // only the body (drag thumb), not a resize handle
            var thumb = (e.Source as Visual)?.FindAncestorOfType<Thumb>(includeSelf: true);
            if (thumb?.Name != "PART_Drag") return;

            var point = e.GetPosition(Root);
            var selectedIndex = slide.SlideElements.IndexOf(_selectedElement);
            for (var i = 0; i < selectedIndex; i++)
            {
                var el = slide.SlideElements[i];
                if (!new Rect(el.X, el.Y, el.Width, el.Height).Contains(point)) continue;

                var proxy = Root.Children.FirstOrDefault(c => ReferenceEquals(c.DataContext, el));
                if (proxy == null) return;

                RemoveSelected();
                AddSelected(proxy, Root);
                _zoomBorder?.Focus();
                e.Handled = true;
                if (e.ClickCount == 2) BeginEditAfterDoubleClick(el);
                return;
            }
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
            _selectedElement = null;
            if (_selected == null)
            {
                return;
            }

            var canvas = Root as Canvas;
            if (canvas != null)
            {
                var layer = AdornerLayerLookup(canvas);
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