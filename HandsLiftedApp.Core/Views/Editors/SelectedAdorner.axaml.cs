using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using HandsLiftedApp.Core.Views.Editors;
using Point = Avalonia.Point;

namespace PerspectiveDemo
{
    public class SelectedAdorner : TemplatedControl
    {
        public static readonly StyledProperty<Control?> ControlProperty =
            AvaloniaProperty.Register<SelectedAdorner, Control?>(nameof(Control));

        public static readonly StyledProperty<ZoomBorder?> ZoomBorderProperty =
            AvaloniaProperty.Register<SelectedAdorner, ZoomBorder?>(nameof(Control));

        private Canvas? _canvas;
        private Thumb? _drag;
        private Rectangle? _border;
        private bool _bodyHitTestable = true;

        /// <summary>
        /// Whether the move area (the invisible thumb that covers the whole element) takes the pointer. Switched off
        /// while the element's text is being typed into, so clicks and text selection reach the text box underneath;
        /// the outline and the resize handles stay on.
        /// </summary>
        public bool BodyHitTestable
        {
            get => _bodyHitTestable;
            set
            {
                _bodyHitTestable = value;
                if (_drag != null) _drag.IsHitTestVisible = value;
            }
        }
        private Thumb? _top;
        private Thumb? _bottom;
        private Thumb? _left;
        private Thumb? _right;
        private Thumb? _topLeft;
        private Thumb? _topRight;
        private Thumb? _bottomLeft;
        private Thumb? _bottomRight;
        // private Thumb? _centre;
        private double _leftOffset;
        private double _topOffset;

        public Control? Control
        {
            get => GetValue(ControlProperty);
            set => SetValue(ControlProperty, value);
        }

        public ZoomBorder? ZoomBorder
        {
            get => GetValue(ZoomBorderProperty);
            set => SetValue(ZoomBorderProperty, value);
        }

        // Slide size and the other elements' rects (slide coordinates) the dragged element snaps to. The provider is
        // asked on every drag move, so the rects are never stale (element geometry/collection can change mid-selection).
        public Size SlideSize { get; set; } = new Size(1920, 1080);
        public Func<IReadOnlyList<Rect>>? SnapTargetsProvider { get; set; }
        private IReadOnlyList<Rect> _snapTargets = Array.Empty<Rect>();

        public IReadOnlyList<Rect> SnapTargets
        {
            get => SnapTargetsProvider?.Invoke() ?? _snapTargets;
            set => _snapTargets = value;
        }

        private Line? verticalLine = null;
        private Line? horizontalLine = null;

        // guideX: x of a vertical guide line, guideY: y of a horizontal guide line; null removes that line.
        private void UpdateSnapLines(double? guideX, double? guideY)
        {
            var rootCanvas = ZoomBorder?.GetControl<Canvas>("Root");
            if (rootCanvas == null) return;

            if (guideX is { } x)
            {
                verticalLine ??= AddGuide(rootCanvas, "VerticalLine");
                verticalLine.StartPoint = new Point(x, 0);
                verticalLine.EndPoint = new Point(x, SlideSize.Height);
            }
            else if (verticalLine != null)
            {
                rootCanvas.Children.Remove(verticalLine);
                verticalLine = null;
            }

            if (guideY is { } y)
            {
                horizontalLine ??= AddGuide(rootCanvas, "HorizontalLine");
                horizontalLine.StartPoint = new Point(0, y);
                horizontalLine.EndPoint = new Point(SlideSize.Width, y);
            }
            else if (horizontalLine != null)
            {
                rootCanvas.Children.Remove(horizontalLine);
                horizontalLine = null;
            }
        }

        private static Line AddGuide(Canvas rootCanvas, string name)
        {
            var line = new Line { Stroke = Brushes.Red, StrokeThickness = 2, Name = name };
            rootCanvas.Children.Add(line);
            return line;
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            UpdateSnapLines(null, null);
            if (_followedControl != null)
            {
                _followedControl.PropertyChanged -= OnFollowedControlPropertyChanged;
                _followedControl = null;
            }

            base.OnDetachedFromLogicalTree(e);
        }

        // The adorner normally drives the control (drag/resize handlers push the new rect into it). The reverse
        // also has to work: when something else moves or resizes the element (undo/redo, typing a value), the
        // handles must follow. Dragging itself sets the control to the adorner's own rect, so the comparison below
        // finds nothing to do and there is no feedback loop.
        private Control? _followedControl;

        private void FollowControl()
        {
            if (Control == null || ReferenceEquals(_followedControl, Control)) return;
            if (_followedControl != null) _followedControl.PropertyChanged -= OnFollowedControlPropertyChanged;
            _followedControl = Control;
            _followedControl.PropertyChanged += OnFollowedControlPropertyChanged;
        }

        private void OnFollowedControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Canvas.LeftProperty || e.Property == Canvas.TopProperty
                || e.Property == WidthProperty || e.Property == HeightProperty)
                SyncHandlesToControl();
        }

        // True while UpdateControl pushes the handles' rect into the control. The control's own change notifications
        // (a diagonal move sets Left before Top; the two-way binding to int X/Y rounds and echoes back) then say nothing
        // new: the handles already hold the true raw/snapped rect, and "syncing" would wipe the drag session.
        private bool _applyingToControl;

        private void SyncHandlesToControl()
        {
            if (_applyingToControl) return;
            if (Control == null || _topLeft == null || _bottomRight == null) return;

            var left = Canvas.GetLeft(Control);
            var top = Canvas.GetTop(Control);
            var width = Control.Width;
            var height = Control.Height;
            if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height)) return;

            var target = new Rect(left, top, width, height);
            var current = GetRect();
            if (Math.Abs(current.X - target.X) < 0.01 && Math.Abs(current.Y - target.Y) < 0.01
                && Math.Abs(current.Width - target.Width) < 0.01 && Math.Abs(current.Height - target.Height) < 0.01)
                return;

            _dragSession = null; // changed from outside the drag: rebase the raw drag position
            UpdateThumbs(target);
            UpdateDrag(target);
        }

        /// <summary>
        /// The rectangle of <paramref name="control"/> in its canvas, from its Canvas.Left/Top and Width/Height rather
        /// than its laid-out Bounds. A control that has only just been added (the proxy of a newly added element,
        /// which is selected in the same dispatch that creates it) has not been measured yet, so its Bounds are
        /// empty and the handles would all start at the canvas origin with a zero-size outline. Falls back to the
        /// Bounds when the control has no explicit position or size.
        /// </summary>
        internal static Rect LogicalRect(Control control)
        {
            var left = Canvas.GetLeft(control);
            var top = Canvas.GetTop(control);
            var width = control.Width;
            var height = control.Height;
            if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height))
                return control.Bounds;
            return new Rect(left, top, width, height);
        }

        /// <summary>The rectangle the handles currently describe (for tests).</summary>
        internal Rect HandlesRect => GetRect();

        private DragSnapSession? _dragSession;
        
        
        public static readonly StyledProperty<Rect?> ClippingBoundsProperty =
            AvaloniaProperty.Register<SelectedAdorner, Rect?>(nameof(ClippingBounds));

        public Rect? ClippingBounds
        {
            get => GetValue(ClippingBoundsProperty);
            set => SetValue(ClippingBoundsProperty, value);
        }

        private void UpdateAdornerTransform()
        {
            if (ZoomBorder != null)
            {
                var zoomX = ZoomBorder.ZoomX;
                var zoomY = ZoomBorder.ZoomY;

                // Apply inverse scaling to all thumb controls to maintain consistent size
                var inverseTransform = new ScaleTransform(1.0 / zoomX, 1.0 / zoomY);

                // Apply transform to all thumb handles
                if (_top != null) _top.RenderTransform = inverseTransform;
                if (_bottom != null) _bottom.RenderTransform = inverseTransform;
                if (_left != null) _left.RenderTransform = inverseTransform;
                if (_right != null) _right.RenderTransform = inverseTransform;
                if (_topLeft != null) _topLeft.RenderTransform = inverseTransform;
                if (_topRight != null) _topRight.RenderTransform = inverseTransform;
                if (_bottomLeft != null) _bottomLeft.RenderTransform = inverseTransform;
                if (_bottomRight != null) _bottomRight.RenderTransform = inverseTransform;

                // The outline is scaled by the zoom with the slide, so counter-scale its stroke to keep a constant
                // 2 screen pixels (the handles above do the same through their inverse transform).
                if (_border != null) _border.StrokeThickness = 2.0 / Math.Max(zoomX, 0.01);

                // Also apply to snap lines if they exist
                if (verticalLine != null) verticalLine.RenderTransform = inverseTransform;
                if (horizontalLine != null) horizontalLine.RenderTransform = inverseTransform;

                // Apply clipping if bounds are set
                UpdateClipping();
            }
        }

        private void UpdateClipping()
        {
            if (ClippingBounds.HasValue && _canvas != null)
            {
                var clippingGeometry = new RectangleGeometry(ClippingBounds.Value);
                _canvas.Clip = clippingGeometry;
                this.Clip = clippingGeometry;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            if (visual is not Canvas)
            {
                return;
            }

            _canvas = e.NameScope.Find<Canvas>("PART_Canvas");
            _drag = e.NameScope.Find<Thumb>("PART_Drag");
            _border = e.NameScope.Find<Rectangle>("PART_Border");
            if (_drag != null) _drag.IsHitTestVisible = _bodyHitTestable;
            _top = e.NameScope.Find<Thumb>("PART_Top");
            _bottom = e.NameScope.Find<Thumb>("PART_Bottom");
            _left = e.NameScope.Find<Thumb>("PART_Left");
            _right = e.NameScope.Find<Thumb>("PART_Right");
            _topLeft = e.NameScope.Find<Thumb>("PART_TopLeft");
            _topRight = e.NameScope.Find<Thumb>("PART_TopRight");
            _bottomLeft = e.NameScope.Find<Thumb>("PART_BottomLeft");
            _bottomRight = e.NameScope.Find<Thumb>("PART_BottomRight");
            // _centre = e.NameScope.Find<Thumb>("PART_Centre");

            // pass-through mouse wheel events
            this.PointerWheelChanged += OnPointerWheelChanged;
            ((Canvas)visual).PointerWheelChanged += OnPointerWheelChanged;
            _canvas.PointerWheelChanged += OnPointerWheelChanged;
                
            // TODO: Also pass through mouse wheel button press event (to allow panning by scroll wheel)

            if (_top is { })
            {
                _top.PointerMoved += OnPointerMovedTop;
            }

            if (_bottom is { })
            {
                _bottom.PointerMoved += OnPointerMovedBottom;
            }

            if (_left is { })
            {
                _left.PointerMoved += OnPointerMovedLeft;
            }

            if (_right is { })
            {
                _right.PointerMoved += OnPointerMovedRight;
            }

            if (_topLeft is { })
            {
                _topLeft.PointerMoved += OnPointerMovedTopLeft;
            }

            if (_topRight is { })
            {
                _topRight.PointerMoved += OnPointerMovedTopRight;
            }

            if (_bottomLeft is { })
            {
                _bottomLeft.PointerMoved += OnPointerMovedBottomLeft;
            }

            if (_bottomRight is { })
            {
                _bottomRight.PointerMoved += OnPointerMovedBottomRight;
            }

            foreach (var corner in new[] { _topLeft, _topRight, _bottomLeft, _bottomRight, _top, _bottom, _left, _right })
            {
                if (corner == null) continue;
                corner.PointerReleased += (_, _) => EndCornerResize();
                corner.PointerCaptureLost += (_, _) => EndCornerResize();
            }

            if (_drag is { })
            {
                _drag.PointerMoved += OnPointerMovedDrag;
                // the gesture ends on release even when the pointer never moves again (and the guide lines come down)
                _drag.PointerReleased += (_, _) => EndDragSession();
                _drag.PointerCaptureLost += (_, _) => EndDragSession();
            }

            if (Control is { })
            {
                _leftOffset = 0; //Canvas.GetLeft(Control);
                _topOffset = 0; //Canvas.GetTop(Control);

                var rect = LogicalRect(Control);

                UpdateThumbs(rect);
                UpdateDrag(rect);

                if (_canvas is { })
                {
                    Canvas.SetLeft(_canvas, rect.Left);
                    Canvas.SetTop(_canvas, rect.Top);
                    _canvas.Width = rect.Width;
                    _canvas.Height = rect.Height;
                    _canvas.HorizontalAlignment = Control.HorizontalAlignment;
                    _canvas.VerticalAlignment = Control.VerticalAlignment;
                }
            }
            
            FollowControl();
            UpdateAdornerTransform();

            // Subscribe to zoom changes
            if (ZoomBorder != null)
            {
                ZoomBorder.ZoomChanged += (s, args) => UpdateAdornerTransform();
            }
        }

        private Point? lastPosition;

        private void ApplyDeltaToThumb(Thumb targetThumb, double deltaX, double deltaY)
        {
            Canvas.SetLeft(targetThumb, Canvas.GetLeft(targetThumb) + deltaX);
            Canvas.SetTop(targetThumb, Canvas.GetTop(targetThumb) + deltaY);
        }
        
        private void OnPointerMovedDrag(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Debug.Print($"OnPointerMovedDrag X={newPosition.X} Y={newPosition.Y}");
                DragBy(newPosition.X - lastPosition.Value.X, newPosition.Y - lastPosition.Value.Y);
            }
            else
            {
                EndDragSession();
            }

            lastPosition = newPosition;
        }

        // The gesture is over (or none is running): the next drag seeds from wherever the element then is, and the
        // snap guides come down.
        private void EndDragSession()
        {
            _dragSession = null;
            UpdateSnapLines(null, null);
        }

        /// <summary>Moves the element by a pointer delta (slide pixels), snapping the raw dragged position.</summary>
        internal void DragBy(double deltaX, double deltaY)
        {
            // Snap the raw (unsnapped) drag position, not the previously snapped rect, or a slow drag sticks.
            _dragSession ??= new DragSnapSession(GetRect());
            var snap = _dragSession.Move(deltaX, deltaY, SnapTargets, SlideSize.Width, SlideSize.Height);
            var draggedRect = snap.Rect;
            UpdateSnapLines(snap.GuideX, snap.GuideY);

            /*
             * ------> X
             * |
             * |
             * V
             * Y
             */

            /*
             * x-----x-----x
             * |           |
             * x           x
             * |           |
             * x-----x-----x
             */

            Canvas.SetLeft(_topLeft, draggedRect.X);
            Canvas.SetTop(_topLeft, draggedRect.Y);
            Canvas.SetLeft(_bottomRight, draggedRect.X + draggedRect.Width);
            Canvas.SetTop(_bottomRight, draggedRect.Y + draggedRect.Height);
            Canvas.SetLeft(_top, draggedRect.X + draggedRect.Width / 2);
            Canvas.SetTop(_top, draggedRect.Y);
            Canvas.SetLeft(_topRight, draggedRect.X + draggedRect.Width);
            Canvas.SetTop(_topRight, draggedRect.Y);
            Canvas.SetLeft(_left, draggedRect.X);
            Canvas.SetTop(_left, draggedRect.Y + draggedRect.Height / 2);
            Canvas.SetLeft(_bottomLeft, draggedRect.X);
            Canvas.SetTop(_bottomLeft, draggedRect.Y + draggedRect.Height);
            Canvas.SetLeft(_bottom, draggedRect.X + draggedRect.Width / 2);
            Canvas.SetTop(_bottom, draggedRect.Y + draggedRect.Height);
            Canvas.SetLeft(_right, draggedRect.X + draggedRect.Width);
            Canvas.SetTop(_right, draggedRect.Y + draggedRect.Height / 2);

            var rect = GetRect();
            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control,
                    rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
            }
        }

        private Rect GetRect()
        {
            var topLeft = new Point(Canvas.GetLeft(_topLeft), Canvas.GetTop(_topLeft));
            var topRight = new Point(Canvas.GetLeft(_topRight), Canvas.GetTop(_topRight));
            var bottomLeft = new Point(Canvas.GetLeft(_bottomLeft), Canvas.GetTop(_bottomLeft));
            var bottomRight = new Point(Canvas.GetLeft(_bottomRight), Canvas.GetTop(_bottomRight));
            var left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
            var width = Math.Abs(right - left);
            var height = Math.Abs(bottom - top);
            return new Rect(left, top, width, height);
        }

        private void UpdateControl(Control control, Rect rect)
        {
            _applyingToControl = true;
            try
            {
                Canvas.SetLeft(control, rect.Left);
                Canvas.SetTop(control, rect.Top);

                control.Width = rect.Width;
                control.Height = rect.Height;
            }
            finally
            {
                _applyingToControl = false;
            }
        }

        private void UpdateThumbs(Rect rect)
        {
            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetTop(_top, rect.Top);

            Canvas.SetLeft(_bottom, rect.Center.X);
            Canvas.SetTop(_bottom, rect.Bottom);

            Canvas.SetLeft(_left, rect.Left);
            Canvas.SetTop(_left, rect.Center.Y);

            Canvas.SetLeft(_right, rect.Right);
            Canvas.SetTop(_right, rect.Center.Y);

            Canvas.SetLeft(_topLeft, rect.Left);
            Canvas.SetTop(_topLeft, rect.Top);

            Canvas.SetLeft(_topRight, rect.Right);
            Canvas.SetTop(_topRight, rect.Top);

            Canvas.SetLeft(_bottomLeft, rect.Left);
            Canvas.SetTop(_bottomLeft, rect.Bottom);

            Canvas.SetLeft(_bottomRight, rect.Right);
            Canvas.SetTop(_bottomRight, rect.Bottom);
        }

        private void UpdateDrag(Rect rect)
        {
            if (_border is { })
            {
                Canvas.SetLeft(_border, rect.Left);
                Canvas.SetTop(_border, rect.Top);

                _border.Width = rect.Width;
                _border.Height = rect.Height;
            }

            if (_drag is { })
            {
                Canvas.SetLeft(_drag, rect.Left);
                Canvas.SetTop(_drag, rect.Top);

                _drag.Width = rect.Width;
                _drag.Height = rect.Height;
            }
        }

        private void OnPointerMovedTop(object? sender, PointerEventArgs e) => ResizeCorner(e, 0, -1);
        private void OnPointerMovedBottom(object? sender, PointerEventArgs e) => ResizeCorner(e, 0, 1);
        private void OnPointerMovedLeft(object? sender, PointerEventArgs e) => ResizeCorner(e, -1, 0);
        private void OnPointerMovedRight(object? sender, PointerEventArgs e) => ResizeCorner(e, 1, 0);

        // One corner-resize gesture: the rect when it started plus the raw (unconstrained) pointer travel since. The
        // size is recomputed from these on every pointer move AND every modifier key change, so pressing Ctrl/Alt/Shift
        // mid-drag snaps to the ratio the element had when the drag began, and releasing it springs back to wherever
        // the pointer is.
        private sealed class CornerResizeSession
        {
            public CornerResizeSession(Rect start, int signX, int signY)
            {
                Start = start;
                SignX = signX;
                SignY = signY;
            }

            public Rect Start { get; }
            public int SignX { get; }   // -1: the left edge moves, +1: the right edge moves
            public int SignY { get; }   // -1: the top edge moves, +1: the bottom edge moves
            public double RawDeltaX { get; set; }
            public double RawDeltaY { get; set; }
            public KeyModifiers Modifiers { get; set; }
            public TopLevel? TopLevel { get; set; }
        }

        private CornerResizeSession? _resizeSession;

        /// <summary>
        /// When true every resize (corner or edge handle) keeps the element's aspect ratio, whether or not a modifier
        /// key is held. Set by the editor's "lock aspect ratio" toggle.
        /// </summary>
        public bool LockAspect { get; set; }

        private static bool KeepsAspect(KeyModifiers modifiers) =>
            (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift)) != 0;

        private void OnPointerMovedTopLeft(object? sender, PointerEventArgs e) => ResizeCorner(e, -1, -1);
        private void OnPointerMovedTopRight(object? sender, PointerEventArgs e) => ResizeCorner(e, 1, -1);
        private void OnPointerMovedBottomLeft(object? sender, PointerEventArgs e) => ResizeCorner(e, -1, 1);
        private void OnPointerMovedBottomRight(object? sender, PointerEventArgs e) => ResizeCorner(e, 1, 1);

        private void ResizeCorner(PointerEventArgs e, int signX, int signY)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (_resizeSession == null) BeginCornerResize(signX, signY);
                var session = _resizeSession!;
                session.RawDeltaX += newPosition.X - lastPosition.Value.X;
                session.RawDeltaY += newPosition.Y - lastPosition.Value.Y;
                session.Modifiers = e.KeyModifiers;
                ApplyCornerResize(session);
            }
            else
            {
                EndCornerResize();
            }

            lastPosition = newPosition;
        }

        private void BeginCornerResize(int signX, int signY)
        {
            _resizeSession = new CornerResizeSession(GetRect(), signX, signY);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            _resizeSession.TopLevel = topLevel;
            // Tunnel so a focused text box etc. can't swallow the modifier change.
            topLevel.AddHandler(InputElement.KeyDownEvent, OnResizeKeyChanged, RoutingStrategies.Tunnel);
            topLevel.AddHandler(InputElement.KeyUpEvent, OnResizeKeyChanged, RoutingStrategies.Tunnel);
        }

        private void EndCornerResize()
        {
            var session = _resizeSession;
            if (session == null) return;
            _resizeSession = null;
            session.TopLevel?.RemoveHandler(InputElement.KeyDownEvent, OnResizeKeyChanged);
            session.TopLevel?.RemoveHandler(InputElement.KeyUpEvent, OnResizeKeyChanged);
        }

        private void OnResizeKeyChanged(object? sender, KeyEventArgs e)
        {
            if (_resizeSession == null) return;
            _resizeSession.Modifiers = e.KeyModifiers;
            ApplyCornerResize(_resizeSession);
        }

        /// <summary>Sizes the element from the session's start rect and raw pointer travel, honouring the modifiers.</summary>
        private void ApplyCornerResize(CornerResizeSession s)
        {
            var width = s.Start.Width + s.SignX * s.RawDeltaX;
            var height = s.Start.Height + s.SignY * s.RawDeltaY;

            if ((LockAspect || KeepsAspect(s.Modifiers)) && s.Start.Width >= 1 && s.Start.Height >= 1)
            {
                var factorX = width / s.Start.Width;
                var factorY = height / s.Start.Height;
                double factor;
                if (s.SignX == 0) factor = factorY;          // top/bottom edge: height drives
                else if (s.SignY == 0) factor = factorX;     // left/right edge: width drives
                else factor = Math.Abs(factorX - 1) >= Math.Abs(factorY - 1) ? factorX : factorY; // corner: further axis wins
                if (factor < 0.01) factor = 0.01; // never collapse or flip the element while locked
                width = s.Start.Width * factor;
                height = s.Start.Height * factor;
            }

            // the opposite corner/edge stays put (an edge drag keeps the other axis centred); the dragged side may
            // cross it (GetRect normalises)
            var left = s.SignX > 0 ? s.Start.Left : s.SignX < 0 ? s.Start.Right - width : s.Start.Center.X - width / 2;
            var right = s.SignX < 0 ? s.Start.Right : left + width;
            var top = s.SignY > 0 ? s.Start.Top : s.SignY < 0 ? s.Start.Bottom - height : s.Start.Center.Y - height / 2;
            var bottom = s.SignY < 0 ? s.Start.Bottom : top + height;

            Canvas.SetLeft(_topLeft, left);     Canvas.SetTop(_topLeft, top);
            Canvas.SetLeft(_topRight, right);   Canvas.SetTop(_topRight, top);
            Canvas.SetLeft(_bottomLeft, left);  Canvas.SetTop(_bottomLeft, bottom);
            Canvas.SetLeft(_bottomRight, right); Canvas.SetTop(_bottomRight, bottom);
            Canvas.SetLeft(_top, (left + right) / 2);    Canvas.SetTop(_top, top);
            Canvas.SetLeft(_bottom, (left + right) / 2); Canvas.SetTop(_bottom, bottom);
            Canvas.SetLeft(_left, left);   Canvas.SetTop(_left, (top + bottom) / 2);
            Canvas.SetLeft(_right, right); Canvas.SetTop(_right, (top + bottom) / 2);

            var rect = GetRect();
            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control,
                    rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            Debug.Print($"OnPointerWheelChanged (Handled!) {sender.GetType()}");
            e.Handled = false;
            // base.OnPointerWheelChanged(e);
        }
    }
}