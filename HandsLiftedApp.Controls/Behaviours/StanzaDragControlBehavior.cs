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
                    ItemsControl listBox = GetParent(target);
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
            if (logicals.Count == 0 || pos.X < 0)
            {
                return -1;
            }

            if (pos.Y < 0 || pos.Y > (itemWidthsByRow.Count) * RowHeight)
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
                    tr.Y = Math.Floor(pos1.Y / RowHeight) * RowHeight;
                }

                _previous = pos1;

                var items = listBox.GetLogicalChildren().Select(x => x as Control).ToList();
                _insertIndex = CalculateInsertPosition(items, pos1);
                    
                if (_insertIndex == -1)
                {
                    ResetHover(GetParent(target)); 
                    return;
                }
                
                ContentPresenter hoveredItem = items[Math.Min(_insertIndex, items.Count - 1)] as ContentPresenter;

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren()
                    .MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);

                // TODO
                bool isPastLastItem =
                    false; // (lastItem != null) && (isPastLastItem = pos1.Y > lastItem.Bounds.Bottom);

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);
                    // Keep items at default z-index except for the hovered one and the dragged container
                    var draggedContainer = GetItem(target);
                    if (listBoxItemContainer != hoveredItem && listBoxItemContainer != draggedContainer)
                    {
                        listBoxItemContainer.ZIndex = 0;
                    }
                }

                // Ensure dragged container always has highest ZIndex
                var draggedContainerFinal = GetItem(target);
                draggedContainerFinal.ZIndex = 1000;

                // Only update adorner if insertion position changed
                if (_insertIndex != _previousInsertIndex)
                {
                    // Clear all adorners from the root layer at once
                    if (_rootAdornerLayer != null)
                    {
                        _rootAdornerLayer.Children.Clear();
                    }

                    if (hoveredItem != target)
                    {
                        hoveredItem.ZIndex = 100;  // Ensure hovered item and adorner are on top
                        var adornerElement = hoveredItem;

                        if (_rootAdornerLayer != null)
                        {
                            var adornedElement = 
                                
                                (_insertIndex >= listBox.ItemCount) ?
                                    new Border()
                                    {
                                        //CornerRadius = new CornerRadius(3, 0, 0, 3),
                                        BorderThickness = new Thickness(0, 0, 4, 0),
                                        BorderBrush = new SolidColorBrush(Color.Parse("#FF4B31"))
                                    }
                                    :
                                new Border()
                            {
                                //CornerRadius = new CornerRadius(3, 0, 0, 3),
                                BorderThickness = new Thickness(4, 0, 0, 0),
                                BorderBrush = new SolidColorBrush(Color.Parse("#FF4B31"))
                            };
                            _rootAdornerLayer.Children.Add(adornedElement);
                            AdornerLayer.SetAdornedElement(adornedElement, adornerElement);
                        }
                    }
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
                
                // Reset ZIndex on the dragged container
                var draggedContainer = GetItem(target);
                draggedContainer.ZIndex = 0;

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;

                UpdateCursor();

                if (_rootAdornerLayer != null)
                {
                    _rootAdornerLayer.Children.Clear();
                }

                _parent = null;
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


