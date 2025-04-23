using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
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

        private Line? verticalLine = null;
        private Line? horizontalLine = null;
        private void UpdateSnapLines(bool showX, bool showY)
        {
            // <Line StartPoint="960,0" EndPoint="960,1080" Stroke="Red" StrokeThickness="2"/>
            // <Line StartPoint="0,540" EndPoint="1920,540" Stroke="Red" StrokeThickness="2"/>
            var rootCanvas = ZoomBorder.GetControl<Canvas>("Root");
            if (showX)
            {
                if (horizontalLine == null)
                {
                    horizontalLine = new Line()
                    {
                        StartPoint = new Point(0, 540), EndPoint = new Point(1920, 540), Stroke = Brushes.Red,
                        StrokeThickness = 2, Name = "HorizontalLine"
                    };
                    rootCanvas.Children.Add(horizontalLine);
                }
            }
            else
            {
                if (horizontalLine != null)
                {
                    rootCanvas.Children.Remove(horizontalLine);
                    horizontalLine = null;
                }
            }
            if (showY)
            {
                if (verticalLine == null)
                {
                    verticalLine = new Line() { StartPoint = new Point(960, 0), EndPoint = new Point(960, 1080), Stroke = Brushes.Red, StrokeThickness = 2, Name = "VerticalLine"};
                    rootCanvas.Children.Add(verticalLine);
                }
            }
            else
            {
                if (verticalLine != null)
                {
                    rootCanvas.Children.Remove(verticalLine);
                    verticalLine = null;
                }
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            UpdateSnapLines(false, false);
            base.OnDetachedFromLogicalTree(e);
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

            if (_drag is { })
            {
                _drag.PointerMoved += OnPointerMovedDrag;
            }

            if (Control is { })
            {
                _leftOffset = 0; //Canvas.GetLeft(Control);
                _topOffset = 0; //Canvas.GetTop(Control);

                var rect = new Rect(
                    Control.Bounds.Left,
                    Control.Bounds.Top,
                    Control.Bounds.Width,
                    Control.Bounds.Height);

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
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;

                var previousRect = GetRect();
                var draggedRect = new Rect(previousRect.X + deltaX, previousRect.Y + deltaY, previousRect.Width, previousRect.Height);

                // Calculate the center of the canvas
                var canvasCenterX = ZoomBorder.GetControl<Canvas>("Root").Bounds.Width / 2;
                var canvasCenterY = ZoomBorder.GetControl<Canvas>("Root").Bounds.Height / 2;

                // Calculate the current position of the dragged element
                var elementCenterX = draggedRect.X + (draggedRect.Width / 2);
                var elementCenterY = draggedRect.Y + (draggedRect.Height / 2);
                
                // Define a snapping threshold
                const double snappingThreshold = 10.0;

                // Check if the element is near the center
                if (Math.Abs(elementCenterX - canvasCenterX) <= snappingThreshold &&
                    Math.Abs(elementCenterY - canvasCenterY) <= snappingThreshold)
                {
                    // Snap to the center
                    draggedRect = new Rect(canvasCenterX - (draggedRect.Width / 2),  canvasCenterY - (draggedRect.Height / 2), previousRect.Width, previousRect.Height);
                    UpdateSnapLines(true, true);
                } 
                else if (Math.Abs(elementCenterX - canvasCenterX) <= snappingThreshold)
                {
                    // Snap to the vertical axis
                    draggedRect = new Rect(canvasCenterX - (draggedRect.Width / 2), draggedRect.Y, previousRect.Width, previousRect.Height);
                    UpdateSnapLines(false, true);
                }
                else if (Math.Abs(elementCenterY - canvasCenterY) <= snappingThreshold)
                {
                    // Snap to the horizontal axis
                    draggedRect = new Rect(draggedRect.X, canvasCenterY - (draggedRect.Height / 2), previousRect.Width, previousRect.Height);
                    UpdateSnapLines(true, false);
                }
                else
                {
                    UpdateSnapLines(false, false);
                }
                // new[] { _top, _bottom, _left, _right, _topLeft, _bottomLeft, _topRight, _bottomRight }
                //     .ToList()
                //     .ForEach(thumb => ApplyDeltaToThumb(thumb, deltaX, deltaY));
                
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
            else
            {
                UpdateSnapLines(false, false);
            }

            lastPosition = newPosition;
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
            Canvas.SetLeft(control, rect.Left);
            Canvas.SetTop(control, rect.Top);

            control.Width = rect.Width;
            control.Height = rect.Height;
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
            if (_drag is { })
            {
                Canvas.SetLeft(_drag, rect.Left);
                Canvas.SetTop(_drag, rect.Top);

                _drag.Width = rect.Width;
                _drag.Height = rect.Height;
            }
        }

        private void OnPointerMovedTop(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetTop(_top, Canvas.GetTop(_top) + deltaY);
                Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + deltaY);
                Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + deltaY);

                var rect = GetRect();

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedBottom(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + deltaY);
                Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + deltaY);
                Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + deltaY);

                var rect = GetRect();

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedLeft(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_left, Canvas.GetLeft(_left) + deltaX);
                Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + deltaX);
                Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + deltaX);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedRight(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_right, Canvas.GetLeft(_right) + deltaX);
                Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + deltaX);
                Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + deltaX);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedTopLeft(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_left, Canvas.GetLeft(_left) + deltaX);
                Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + deltaX);
                Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + deltaX);

                Canvas.SetTop(_top, Canvas.GetTop(_top) + deltaY);
                Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + deltaY);
                Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + deltaY);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedTopRight(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_right, Canvas.GetLeft(_right) + deltaX);
                Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + deltaX);
                Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + deltaX);

                Canvas.SetTop(_top, Canvas.GetTop(_top) + deltaY);
                Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + deltaY);
                Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + deltaY);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedBottomLeft(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_left, Canvas.GetLeft(_left) + deltaX);
                Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + deltaX);
                Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + deltaX);

                Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + deltaY);
                Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + deltaY);
                Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + deltaY);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerMovedBottomRight(object? sender, PointerEventArgs e)
        {
            var visual = GetValue(AdornerLayer.AdornedElementProperty);
            var newPosition = e.GetPosition(visual);

            if (lastPosition is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var deltaX = newPosition.X - lastPosition.Value.X;
                var deltaY = newPosition.Y - lastPosition.Value.Y;
                
                Canvas.SetLeft(_right, Canvas.GetLeft(_right) + deltaX);
                Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + deltaX);
                Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + deltaX);

                Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + deltaY);
                Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + deltaY);
                Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + deltaY);

                var rect = GetRect();

                Canvas.SetLeft(_top, rect.Center.X);
                Canvas.SetLeft(_bottom, rect.Center.X);

                Canvas.SetTop(_left, rect.Center.Y);
                Canvas.SetTop(_right, rect.Center.Y);

                UpdateDrag(rect);

                if (Control is { })
                {
                    UpdateControl(Control,
                        rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset, _topOffset)));
                }
            }

            lastPosition = newPosition;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            Debug.Print($"OnPointerWheelChanged (Handled!) {sender.GetType()}");
            e.Handled = false;
            // base.OnPointerWheelChanged(e);
        }
    }
}