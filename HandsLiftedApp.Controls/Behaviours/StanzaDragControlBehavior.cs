using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Behaviours;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using Serilog;

namespace HandsLiftedApp.Controls.Behaviours
{
    /// <summary>
    /// A behavior that allows controls to be moved around the canvas using RenderTransform of <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    public sealed class StanzaDragControlBehavior : Behavior<Control>
    {
        /// <summary>
        /// Identifies the <seealso cref="TargetControl"/> avalonia property.
        /// </summary>
        public static readonly StyledProperty<Control?> TargetControlProperty =
            AvaloniaProperty.Register<DragControlBehavior, Control?>(nameof(TargetControl));

        private Control? _parent;
        private Point _previous;
        private int _insertIndex;
        private int _previousInsertIndex = -1;
        private int _sourceIndex = -1;
        private Control? _placeholder;

        /// <summary>
        /// Gets or sets the target control to be moved around instead of <see cref="IBehavior.AssociatedObject"/>. This is a avalonia property.
        /// </summary>
        [ResolveByName]
        public Control? TargetControl
        {
            get => GetValue(TargetControlProperty);
            set => SetValue(TargetControlProperty, value);
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree()
        {
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            var source = AssociatedObject;
            if (source is { })
            {
                source.PointerPressed += Source_PointerPressed;
                
                if (source.ContextMenu != null)
                {
                    source.ContextMenu.Opening += (sender, args) =>
                    {
                        ResetDraggingState();
                        ItemsControl listBox = GetParent(source);
                        ResetOpacities(listBox);
                    };
                }
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            var source = AssociatedObject;
            if (source is { })
            {
                source.PointerPressed -= Source_PointerPressed;
            }

            _parent = null;
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree()
        {
        }

        private ContentPresenter GetItem(Control target) => ControlExtension.FindAncestor<ContentPresenter>(target);
        private ItemsControl GetParent(Control target) => ControlExtension.FindAncestor<ItemsControl>(target);

        private void Source_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var draggedItem = TargetControl ?? AssociatedObject;
            if (draggedItem is { })
            {
                _parent = GetParent(draggedItem);

                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    var listBox = GetParent(draggedItem);
                    int sourceIndex = listBox.IndexFromContainer(GetItem(draggedItem));

                    SongItem ctx = (SongItem)listBox.DataContext;
                    ctx.Arrangement.RemoveAt(sourceIndex);
                    return;
                }

                if (!(draggedItem.RenderTransform is TranslateTransform))
                {
                    draggedItem.RenderTransform = new TranslateTransform();
                }

                _previous = e.GetPosition(_parent);
                if (_parent != null)
                {
                    _parent.PointerMoved += Parent_PointerMoved;
                    _parent.PointerReleased += Parent_PointerReleased;

                    draggedItem.Opacity = 0.8;
                    
                    // Find the container for the dragged item and set its ZIndex
                    var draggedContainer = GetItem(draggedItem);
                    draggedContainer.ZIndex = 1000;  // Bring dragged item container to front

                    var visualRoot = _parent.GetVisualRoot();
                    if (visualRoot is Window window)
                    {
                        window.LostFocus += StanzaDragControlBehavior_LostFocus;
                        window.PointerPressed += StanzaDragControlBehavior_PointerPressed;
                    }
                    
                    // Get the adorner layer from the parent ItemsControl
                    _rootAdornerLayer = AdornerLayer.GetAdornerLayer(_parent);
                }
                
                var items = GetParent(draggedItem).GetLogicalChildren().OfType<Control>().ToList();
                CalculateItemPositions(items);

                // Track the source index of the dragged item
                var parentListBox = GetParent(draggedItem);
                _sourceIndex = parentListBox.IndexFromContainer(GetItem(draggedItem));

                _insertIndex = -1;
            }
        }

        private void StanzaDragControlBehavior_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsRightButtonPressed)
            {
                ResetDraggingState();
            }
        }

        private void StanzaDragControlBehavior_LostFocus(object? sender, RoutedEventArgs e)
        {
            ResetDraggingState();
        }

        private List<List<double>> itemWidthsByRow = new();
        private double RowHeight { get; set; }
        private AdornerLayer? _rootAdornerLayer;

        private void CalculateItemPositions(List<Control> items)
        {
            itemWidthsByRow = new();
            if (items.Count == 0) return;

            var firstItem = items.First();
            var lastY = firstItem.Bounds.Y;
            RowHeight = firstItem.Bounds.Height;
            
            var currentRow = 0;
            foreach (var item in items)
            {
                if (item.Bounds.Y != lastY)
                {
                    lastY = item.Bounds.Y;
                    currentRow++;
                }

                if (itemWidthsByRow.ElementAtOrDefault(currentRow) == null)
                {
                    itemWidthsByRow.Add(new List<double>());
                }

                itemWidthsByRow[currentRow].Add(item.Bounds.Width);
            }
        }

        private int CalculateInsertPosition(List<Control> items, Point pos)
        {
            if (items.Count == 0)
            {
                return -1;
            }

            // If the position is above the first row, it might be the first item
            if (pos.Y < 0)
            {
                return -1;
            }

            var idx = 0;
            var row = 0;
            foreach (var rowWidths in itemWidthsByRow)
            {
                double rowX = 0;
                double totalRowHeight = (row + 1) * RowHeight;
                
                foreach (var width in rowWidths)
                {
                    if (pos.Y <= totalRowHeight)
                    {
                        if (pos.X < (rowX + width))
                        {
                            return idx;
                        }
                    }

                    rowX += width;
                    idx++;
                }

                // If pointer is past the last item on this row, but still on this row
                if (pos.Y <= totalRowHeight)
                {
                    return idx;
                }
                
                row++;
            }

            // If we've gone through all rows and haven't returned, we're likely past the last row
            return items.Count;
        }

        void ResetHover(ItemsControl listBox)
        {
            if (_rootAdornerLayer != null)
            {
                _rootAdornerLayer.Children.Clear();
                _placeholder = null;
            }
        }

        private Point GetPositionAtIndex(int index)
        {
            var currentIdx = 0;
            var currentRow = 0;
            
            if (itemWidthsByRow.Count == 0)
                return new Point(0, 0);

            foreach (var rowKV in itemWidthsByRow)
            {
                double rowX = 0;
                double rowY = currentRow * RowHeight;

                foreach (var width in rowKV)
                {
                    if (currentIdx == index)
                    {
                        return new Point(rowX, rowY);
                    }

                    rowX += width;
                    currentIdx++;
                }

                if (currentIdx == index)
                {
                    return new Point(rowX, rowY);
                }

                currentRow++;
            }

            return new Point(0, 0);
        }

        private void UpdateItemPreview(ItemsControl listBox, Control draggedItem, int sourceIndex, int destinationIndex)
        {
            // Reset all items to their normal state
            for (int i = 0; i < listBox.ItemCount; i++)
            {
                var container = listBox.ContainerFromIndex(i);
                if (container is Control ctrl)
                {
                    ctrl.ZIndex = 0;
                    ctrl.Opacity = 1.0;
                    
                    // Clear any render transforms
                    if (ctrl.RenderTransform is TranslateTransform tr)
                    {
                        tr.X = 0;
                        tr.Y = 0;
                    }
                }
            }

            // Get the dragged container
            var draggedContainer = GetItem(draggedItem);
            draggedContainer.ZIndex = 1000;
            draggedContainer.Opacity = 0.8; // Keep dragged item semi-transparent

            // Draw placeholder in adorner layer where the item would be dropped
            if (_rootAdornerLayer != null)
            {
                if (_placeholder == null)
                {
                    _placeholder = new Border
                    {
                        Background = Brushes.LightBlue,
                        Opacity = 0.3,
                        BorderBrush = Brushes.DodgerBlue,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2),
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        RenderTransform = new TranslateTransform()
                    };
                    _rootAdornerLayer.Children.Add(_placeholder);
                }

                _placeholder.Width = draggedContainer.Bounds.Width;
                _placeholder.Height = draggedContainer.Bounds.Height;

                Point destPos = GetPositionAtIndex(destinationIndex);

                // Translate destPos from listBox to _rootAdornerLayer coordinates
                var listBoxPos = listBox.TranslatePoint(destPos, _rootAdornerLayer);
                if (listBoxPos.HasValue && _placeholder.RenderTransform is TranslateTransform tt)
                {
                    tt.X = listBoxPos.Value.X;
                    tt.Y = listBoxPos.Value.Y;
                }
            }

            // Determine the range of items that need to shift
            int minIndex = Math.Min(sourceIndex, destinationIndex);
            int maxIndex = Math.Max(sourceIndex, destinationIndex);

            // Shift items to create visual preview
            if (sourceIndex < destinationIndex)
            {
                // Dragging forward: shift items between source and destination backward
                int shiftEndIndex = Math.Min(destinationIndex, listBox.ItemCount - 1);
                for (int i = sourceIndex + 1; i <= shiftEndIndex; i++)
                {
                    var container = listBox.ContainerFromIndex(i);
                    if (container is Control ctrl && ctrl != draggedContainer)
                    {
                        // Shift backward to make room
                        if (ctrl.RenderTransform is TranslateTransform tr)
                        {
                            tr.X = -(draggedContainer.Bounds.Width > 0 ? draggedContainer.Bounds.Width : 100);
                        }
                        else
                        {
                            ctrl.RenderTransform = new TranslateTransform 
                            { 
                                X = -(draggedContainer.Bounds.Width > 0 ? draggedContainer.Bounds.Width : 100)
                            };
                        }
                    }
                }
            }
            else if (sourceIndex > destinationIndex)
            {
                // Dragging backward: shift items between destination and source forward
                for (int i = destinationIndex; i < sourceIndex && i < listBox.ItemCount; i++)
                {
                    var container = listBox.ContainerFromIndex(i);
                    if (container is Control ctrl && ctrl != draggedContainer)
                    {
                        // Shift forward to make room
                        if (ctrl.RenderTransform is TranslateTransform tr)
                        {
                            tr.X = (draggedContainer.Bounds.Width > 0 ? draggedContainer.Bounds.Width : 100);
                        }
                        else
                        {
                            ctrl.RenderTransform = new TranslateTransform 
                            { 
                                X = (draggedContainer.Bounds.Width > 0 ? draggedContainer.Bounds.Width : 100)
                            };
                        }
                    }
                }
            }
        }

        private void Parent_PointerMoved(object? sender, PointerEventArgs args)
        {
            var draggedItem = TargetControl ?? AssociatedObject;
            if (args.GetCurrentPoint(draggedItem).Properties.IsRightButtonPressed)
            {
                ResetDraggingState();

                if (draggedItem == null)
                {
                    return;
                }

                ResetHover(GetParent(draggedItem)); 

                return;
            }

            UpdateCursor(new Cursor(StandardCursorType.DragMove));

            if (draggedItem is { })
            {
                ItemsControl listBox = GetParent(draggedItem);

                Point currentPos = args.GetPosition(_parent);
                if (draggedItem.RenderTransform is TranslateTransform tr)
                {
                    tr.X += currentPos.X - _previous.X;
                    tr.Y += currentPos.Y - _previous.Y;
                }

                _previous = currentPos;

                var items = listBox.GetLogicalChildren().OfType<Control>().ToList();
                int newInsertIndex = CalculateInsertPosition(items, currentPos);
                
                // If the mouse is outside the bounds, reset preview insert index to the original source position
                if (newInsertIndex == -1)
                {
                    newInsertIndex = _sourceIndex;
                }
                
                // Only update preview if insertion position changed
                if (newInsertIndex != _previousInsertIndex)
                {
                    _insertIndex = newInsertIndex;
                    UpdateItemPreview(listBox, draggedItem, _sourceIndex, _insertIndex);
                }

                _previousInsertIndex = _insertIndex;
            }
        }

        private void UpdateCursor(Cursor? cursor = null, Control? p = null)
        {
            if (p == null)
                p = _parent;

            Window? window = p?.GetVisualRoot() as Window;
            if (window != null)
            {
                window.Cursor = (cursor == null) ? Cursor.Default : cursor;
            }
        }

        private void ResetDraggingState()
        {
            if (_parent is { })
            {
                var draggedItem = TargetControl ?? AssociatedObject;
                draggedItem.RenderTransform = new TranslateTransform();
                draggedItem.Opacity = 1.0;
                
                // Reset ZIndex on the dragged container
                var draggedContainer = GetItem(draggedItem);
                draggedContainer.ZIndex = 0;

                // Reset all items to normal state
                ItemsControl listBox = GetParent(draggedItem);
                if (listBox != null)
                {
                    for (int i = 0; i < listBox.ItemCount; i++)
                    {
                        var container = listBox.ContainerFromIndex(i);
                        if (container is Control ctrl)
                        {
                            ctrl.Opacity = 1.0;
                            ctrl.ZIndex = 0;
                            if (ctrl.RenderTransform is TranslateTransform tr)
                            {
                                tr.X = 0;
                                tr.Y = 0;
                            }
                        }
                    }
                }

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;

                UpdateCursor();

                if (_rootAdornerLayer != null)
                {
                    _rootAdornerLayer.Children.Clear();
                    _placeholder = null;
                }

                _parent = null;
                _sourceIndex = -1;
            }
        }

        private void ResetOpacities(ItemsControl listBox)
        {
            for (int i = 0; i < listBox.ItemCount; i++)
            {
                var container = listBox.ContainerFromIndex(i);
                if (container != null)
                {
                    container.Classes.Remove("draggingover");
                    var dockPanel = container.FindDescendantOfType<DockPanel>();
                    if (dockPanel != null)
                    {
                        dockPanel.Opacity = 1;
                    }
                }
            }

            if (_rootAdornerLayer != null)
            {
                _rootAdornerLayer.Children.Clear();
                _placeholder = null;
            }
        }

        private void Parent_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_parent is { })
            {
                var draggedItem = TargetControl ?? AssociatedObject;

                ItemsControl listBox = GetParent(draggedItem);

                int sourceIndex = listBox.IndexFromContainer(GetItem(draggedItem));
                int destinationIndex = _insertIndex;

                ResetOpacities(listBox);
                ResetDraggingState();
                
                if (sourceIndex != destinationIndex
                    && sourceIndex > -1
                    && destinationIndex > -1)
                {
                    SongItem ctx = (SongItem)listBox.DataContext;

                    try
                    {
                        ctx.Arrangement.Move(sourceIndex, destinationIndex);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Failed to move item", ex);
                    }
                }
            }
        }
    }
}


