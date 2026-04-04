using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                _parent = GetParent(target);

                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    var listBox = GetParent(target);
                    int SourceIndex = listBox.IndexFromContainer(GetItem(target));

                    SongItem ctx = (SongItem)listBox.DataContext;
                    ctx.Arrangement.RemoveAt(SourceIndex);
                    return;
                }

                if (!(target.RenderTransform is TranslateTransform))
                {
                    target.RenderTransform = new TranslateTransform();
                }

                _previous = e.GetPosition(_parent);
                if (_parent != null)
                {
                    _parent.PointerMoved += Parent_PointerMoved;
                    _parent.PointerReleased += Parent_PointerReleased;

                    target.Opacity = 0.8;
                    
                    // Find the container for the dragged item and set its ZIndex
                    var draggedContainer = GetItem(target);
                    draggedContainer.ZIndex = 1000;  // Bring dragged item container to front

                    var m = _parent.GetVisualRoot();
                    if (m is Window window)
                    {
                        window.LostFocus += StanzaDragControlBehavior_LostFocus;
                        window.PointerPressed += StanzaDragControlBehavior_PointerPressed;
                    }
                    
                    // Get the adorner layer from the parent ItemsControl
                    _rootAdornerLayer = AdornerLayer.GetAdornerLayer(_parent);
                }
                
                var items = GetParent(target).GetLogicalChildren().Select(x => x as Control).ToList();
                CalculateItemPositions(items);

                // Track the source index of the dragged item
                var parentListBox = GetParent(target);
                _sourceIndex = parentListBox.IndexFromContainer(GetItem(target));

                _insertIndex = -1;
                _previousInsertIndex = -1;
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

        private void CalculateItemPositions(List<Control> logicals)
        {
            itemWidthsByRow = new();
            var lastY = (logicals.First() as Control).Bounds.Y;
            RowHeight = (logicals.First() as Control).Bounds.Height;
            
            var thisRow = 0;
            foreach (var VARIABLE in logicals)
            {
                Control curr = (VARIABLE as Control);

                if ((VARIABLE as ContentPresenter).Bounds.Y != lastY)
                {
                    lastY = (VARIABLE as ContentPresenter).Bounds.Y;
                    thisRow++;
                }

                if (itemWidthsByRow.ElementAtOrDefault(thisRow) == null)
                {
                    itemWidthsByRow.Add(new List<double>());
                }

                itemWidthsByRow[thisRow].Add(curr.Bounds.Width);
            }
        }

        private int CalculateInsertPosition(List<Control> logicals, Point pos)
        {
            if (logicals.Count == 0)
            {
                return -1;
            }

            if (pos.X < 0 || pos.Y < 0 || pos.Y > (itemWidthsByRow.Count) * RowHeight)
            {
                return -1;
            }

            // use map
            var idx = 0;
            var row = 0;
            foreach (var rowKV in itemWidthsByRow)
            {
                double rowX = 0;
                double totalRowHeight = (row + 1)* RowHeight;
                
                foreach (var VARIABLE in rowKV)
                {

                    if (pos.X < (rowX + VARIABLE / 3 * 2) && pos.Y <= totalRowHeight)
                    {
                        return idx;
                    }
                    if (pos.X < (rowX + VARIABLE) && pos.Y <= totalRowHeight)
                    {
                        return idx + 1;
                    }

                    rowX += VARIABLE;
                    idx++;
                }

                if (pos.Y <= totalRowHeight)
                {
                    return idx; // ?
                }
                
                row++;
            }

            return idx;
        }

        void ResetHover(ItemsControl listBox)
        {
            if (_rootAdornerLayer != null)
            {
                _rootAdornerLayer.Children.Clear();
            }
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

            // Determine the range of items that need to shift
            int minIndex = Math.Min(sourceIndex, destinationIndex);
            int maxIndex = Math.Max(sourceIndex, destinationIndex);

            // Shift items to create visual preview
            if (sourceIndex < destinationIndex)
            {
                // Dragging forward: shift items between source and destination backward
                for (int i = sourceIndex + 1; i <= destinationIndex && i < listBox.ItemCount; i++)
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

            // Make the source item's space invisible (collapse it)
            if (sourceIndex >= 0 && sourceIndex < listBox.ItemCount)
            {
                var sourceContainer = listBox.ContainerFromIndex(sourceIndex);
                if (sourceContainer is Control sourceCtrl && sourceCtrl != draggedContainer)
                {
                    sourceCtrl.Opacity = 0.0; // Hide the original position
                }
            }
        }

        private void Parent_PointerMoved(object? sender, PointerEventArgs args)
        {
            var target = TargetControl ?? AssociatedObject;
            if (args.GetCurrentPoint(target).Properties.IsRightButtonPressed)
            {
                ResetDraggingState();

                if (target == null)
                {
                    return;
                }

                ResetHover(GetParent(target)); 

                return;
            }

            UpdateCursor(new Cursor(StandardCursorType.DragMove));

            if (target is { })
            {
                ItemsControl listBox = GetParent(target);

                Point pos1 = args.GetPosition(_parent);
                if (target.RenderTransform is TranslateTransform tr)
                {
                    tr.X += pos1.X - _previous.X;
                    tr.Y += pos1.Y - _previous.Y;
                }

                _previous = pos1;

                var items = listBox.GetLogicalChildren().Select(x => x as Control).ToList();
                int newInsertIndex = CalculateInsertPosition(items, pos1);
                
                // If the mouse is outside the bounds, reset preview insert index to the original source position
                if (newInsertIndex == -1)
                {
                    newInsertIndex = _sourceIndex;
                }
                
                // Only update preview if insertion position changed
                if (newInsertIndex != _previousInsertIndex)
                {
                    _insertIndex = newInsertIndex;
                    UpdateItemPreview(listBox, target, _sourceIndex, _insertIndex);
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
                var target = TargetControl ?? AssociatedObject;
                target.RenderTransform = new TranslateTransform();
                target.Opacity = 1.0;
                
                // Reset ZIndex on the dragged container
                var draggedContainer = GetItem(target);
                draggedContainer.ZIndex = 0;

                // Reset all items to normal state
                ItemsControl listBox = GetParent(target);
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
                }

                _parent = null;
                _sourceIndex = -1;
            }
        }

        private void ResetOpacities(ItemsControl listBox)
        {
            for (int i = 0; i < listBox.ItemCount; i++)
            {
                var listBoxItemContainer = listBox.ContainerFromIndex(i);
                listBoxItemContainer.Classes.Remove("draggingover");
                listBoxItemContainer.FindDescendantOfType<DockPanel>().Opacity = 1;
            }

            if (_rootAdornerLayer != null)
            {
                _rootAdornerLayer.Children.Clear();
            }
        }

        private void Parent_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_parent is { })
            {
                var target = TargetControl ?? AssociatedObject;

                ItemsControl listBox = GetParent(target);

                int SourceIndex =
                    listBox.ItemContainerGenerator.IndexFromContainer(((Control)target)
                        .FindAncestorOfType<ContentPresenter>());
                int DestinationIndex = _insertIndex;

                ResetOpacities(listBox);
                ResetDraggingState();
                if (SourceIndex != DestinationIndex
                    && SourceIndex > -1
                    && DestinationIndex > -1)
                {
                    //Debug.Print($"Moved {SourceIndex} to {DestinationIndex}, isPastLastItem: {isPastLastItem}");
                    SongItem ctx = (SongItem)listBox.DataContext;

                    try
                    {
                        if (DestinationIndex > SourceIndex)
                        {
                            // to account for this current item's slot being removed when shifting forwards this item
                            DestinationIndex--;
                        }
                        ctx.Arrangement.Move(SourceIndex, Math.Min(DestinationIndex, ctx.Arrangement.Count - 1));
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


