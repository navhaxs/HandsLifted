﻿using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace PerspectiveDemo
{
    public class SelectedAdorner : TemplatedControl
    {
        public static readonly StyledProperty<Control?> ControlProperty = 
            AvaloniaProperty.Register<SelectedAdorner, Control?>(nameof(Control));

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
        private double _leftOffset;
        private double _topOffset;

        public Control? Control
        {
            get => GetValue(ControlProperty);
            set => SetValue(ControlProperty, value);
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

            if (_top is { })
            {
                _top.DragDelta += OnDragDeltaTop;
            }

            if (_bottom is { })
            {
                _bottom.DragDelta += OnDragDeltaBottom;
            }

            if (_left is { })
            {
                _left.DragDelta += OnDragDeltaLeft;
            }

            if (_right is { })
            {
                _right.DragDelta += OnDragDeltaRight;
            }

            if (_topLeft is { })
            {
                _topLeft.DragDelta += OnDragDeltaTopLeft;
            }

            if (_topRight is { })
            {
                _topRight.DragDelta += OnDragDeltaTopRight;
            }

            if (_bottomLeft is { })
            {
                _bottomLeft.DragDelta += OnDragDeltaBottomLeft;
            }
 
            if (_bottomRight is { })
            {
                _bottomRight.DragDelta += OnDragDeltaBottomRight;
            }

            if (_drag is { })
            {
                _drag.DragDelta += OnDragDeltaDrag;
            }

            if (Control is { })
            {
                _leftOffset = Canvas.GetLeft(Control);
                _topOffset = Canvas.GetTop(Control);

                var rect = new Rect(
                    0,
                    0,
                    Control.Width, 
                    Control.Height);

                UpdateThumbs(rect);
                UpdateDrag(rect);

                if (_canvas is { })
                {
                    Canvas.SetLeft(_canvas, rect.Left);
                    Canvas.SetTop(_canvas, rect.Top);
                    _canvas.Width = rect.Width;
                    _canvas.Height = rect.Height;
                }
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

        private void OnDragDeltaDrag(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_top, Canvas.GetLeft(_top) + e.Vector.X);
            Canvas.SetLeft(_bottom, Canvas.GetLeft(_bottom) + e.Vector.X);

            Canvas.SetLeft(_left, Canvas.GetLeft(_left) + e.Vector.X);
            Canvas.SetLeft(_right, Canvas.GetLeft(_right) + e.Vector.X);

            Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + e.Vector.X);
            Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + e.Vector.X);

            Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + e.Vector.X);
            Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + e.Vector.X);

            Canvas.SetTop(_top, Canvas.GetTop(_top) + e.Vector.Y);
            Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + e.Vector.Y);

            Canvas.SetTop(_left, Canvas.GetTop(_left) + e.Vector.Y);
            Canvas.SetTop(_right, Canvas.GetTop(_right) + e.Vector.Y);

            Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + e.Vector.Y);
            Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + e.Vector.Y);

            Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + e.Vector.Y);
            Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + e.Vector.Y);

            var rect = GetRect();

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaTop(object? sender, VectorEventArgs e)
        {
            Canvas.SetTop(_top, Canvas.GetTop(_top) + e.Vector.Y);
            Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + e.Vector.Y);
            Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + e.Vector.Y);

            var rect = GetRect();

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);
  
            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaBottom(object? sender, VectorEventArgs e)
        {
            Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + e.Vector.Y);
            Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + e.Vector.Y);
            Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + e.Vector.Y);

            var rect = GetRect();

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaLeft(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_left, Canvas.GetLeft(_left) + e.Vector.X);
            Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + e.Vector.X);
            Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + e.Vector.X);

            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaRight(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_right, Canvas.GetLeft(_right) + e.Vector.X);
            Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + e.Vector.X);
            Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + e.Vector.X);

            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }
 
        private void OnDragDeltaTopLeft(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_left, Canvas.GetLeft(_left) + e.Vector.X);
            Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + e.Vector.X);
            Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + e.Vector.X);

            Canvas.SetTop(_top, Canvas.GetTop(_top) + e.Vector.Y);
            Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + e.Vector.Y);
            Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + e.Vector.Y);

            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaTopRight(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_right, Canvas.GetLeft(_right) + e.Vector.X);
            Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + e.Vector.X);
            Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + e.Vector.X);

            Canvas.SetTop(_top, Canvas.GetTop(_top) + e.Vector.Y);
            Canvas.SetTop(_topLeft, Canvas.GetTop(_topLeft) + e.Vector.Y);
            Canvas.SetTop(_topRight, Canvas.GetTop(_topRight) + e.Vector.Y);
            
            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaBottomLeft(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_left, Canvas.GetLeft(_left) + e.Vector.X);
            Canvas.SetLeft(_topLeft, Canvas.GetLeft(_topLeft) + e.Vector.X);
            Canvas.SetLeft(_bottomLeft, Canvas.GetLeft(_bottomLeft) + e.Vector.X);

            Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + e.Vector.Y);
            Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + e.Vector.Y);
            Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + e.Vector.Y);

            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }

        private void OnDragDeltaBottomRight(object? sender, VectorEventArgs e)
        {
            Canvas.SetLeft(_right, Canvas.GetLeft(_right) + e.Vector.X);
            Canvas.SetLeft(_topRight, Canvas.GetLeft(_topRight) + e.Vector.X);
            Canvas.SetLeft(_bottomRight, Canvas.GetLeft(_bottomRight) + e.Vector.X);

            Canvas.SetTop(_bottom, Canvas.GetTop(_bottom) + e.Vector.Y);
            Canvas.SetTop(_bottomLeft, Canvas.GetTop(_bottomLeft) + e.Vector.Y);
            Canvas.SetTop(_bottomRight, Canvas.GetTop(_bottomRight) + e.Vector.Y);

            var rect = GetRect();

            Canvas.SetLeft(_top, rect.Center.X);
            Canvas.SetLeft(_bottom, rect.Center.X);

            Canvas.SetTop(_left, rect.Center.Y);
            Canvas.SetTop(_right, rect.Center.Y);

            UpdateDrag(rect);

            if (Control is { })
            {
                UpdateControl(Control, rect.Inflate(new Thickness(-_leftOffset, -_topOffset, _leftOffset , _topOffset)));
            }
        }
    }
}
