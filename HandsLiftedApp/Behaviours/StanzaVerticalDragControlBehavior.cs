using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.ViewModels.Editor;
using System;
using System.Linq;

namespace HandsLiftedApp.Behaviours
{
    /// <summary>
    /// A behavior that allows controls to be moved around the canvas using RenderTransform of <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    public sealed class StanzaVerticalDragControlBehavior : Behavior<Control>
    {
        /// <summary>
        /// Identifies the <seealso cref="TargetControl"/> avalonia property.
        /// </summary>
        public static readonly StyledProperty<Control?> TargetControlProperty =
            AvaloniaProperty.Register<DragControlBehavior, Control?>(nameof(TargetControl));

        private Control? _parent;
        private Point _previous;
        private bool isDragging = false;

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
                    source.ContextMenu.Opened += ContextMenu_MenuOpened;
                }
            }
        }

        private void ContextMenu_MenuOpened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CancelDrag();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            var source = AssociatedObject;
            if (source is { })
            {
                source.PointerPressed -= Source_PointerPressed;

                if (source.ContextMenu != null)
                {
                    source.ContextMenu.Opened -= ContextMenu_MenuOpened;
                }
            }

            _parent = null;
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree()
        {
        }

        private void Source_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                ItemsControl listBox = ControlExtension.FindAncestor<ItemsControl>(target);
                _parent = listBox;

                ContentPresenter thisItem = ControlExtension.FindAncestor<ContentPresenter>(target);
                if (!(thisItem.RenderTransform is TranslateTransform))
                {
                    thisItem.RenderTransform = new TranslateTransform();
                }

                _previous = e.GetPosition(_parent);
                if (_parent != null)
                {
                    _parent.PointerMoved += Parent_PointerMoved;
                    _parent.PointerReleased += Parent_PointerReleased;
                }

                thisItem.Opacity = 0.5;
                thisItem.ZIndex = 999;
            }
        }

        private ContentPresenter GetHoveredItem(ItemsControl listBox, Point pos, Control? target)
        {
            var children = listBox.GetLogicalChildren(); // ContentPresenter

            var visualsAtPos = listBox.GetVisualsAt(pos); // Border
            var itemsAtPos = visualsAtPos.Select(visual => ControlExtension.FindAncestor<ContentPresenter>((Control)visual)).ToArray(); // ContentPresenter

            return (ContentPresenter)children.Where(listBoxItem => listBoxItem != target).FirstOrDefault(childItem => itemsAtPos.Contains(childItem));
        }


        private void CancelDrag()
        {
            var target = TargetControl ?? AssociatedObject;

            UpdateCursor(false);

            if (_parent != null)
            {
                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;
                _parent = null;
            }

            isDragging = false;

            if (target != null)
            {
                target.RenderTransform = new TranslateTransform();
                ItemsControl listBox = ControlExtension.FindAncestor<ItemsControl>(target);

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);

                    listBoxItemContainer.Classes.Remove("draggingover");
                    listBoxItemContainer.Opacity = 1;
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItemContainer);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }
                }
            }
        }

        private void Parent_PointerMoved(object? sender, PointerEventArgs args)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                if (args.GetCurrentPoint(target).Properties.IsRightButtonPressed)
                {
                    CancelDrag();
                    return;
                }
            }
            if (isDragging)
            {
                UpdateCursor(true);
            }
            if (target is { })
            {
                Point pos = args.GetPosition(_parent);

                if (!isDragging && Math.Abs(pos.Y - _previous.Y) > 4)
                {
                    isDragging = true;
                }

                if (!isDragging)
                {
                    return;
                }

                ContentPresenter thisItem = ControlExtension.FindAncestor<ContentPresenter>(target);
                if (thisItem.RenderTransform is TranslateTransform tr)
                {
                    //tr.X += pos.X - _previous.X;
                    tr.Y += pos.Y - _previous.Y;
                }
                _previous = pos;

                ItemsControl listBox = ControlExtension.FindAncestor<ItemsControl>(target);
                ContentPresenter hoveredItem = GetHoveredItem(listBox, pos, thisItem);

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom)
                    && pos.X > 0 && pos.X < listBox.Bounds.Width;

                ContentPresenter? firstItem = (ContentPresenter)listBox.GetLogicalChildren().MinBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Top);
                bool isBeforeFirstItem = (firstItem != null) && (isBeforeFirstItem = pos.Y < firstItem.Bounds.Top);

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItemContainer);
                    adornerLayer.Children.Clear();

                    if (listBoxItemContainer != thisItem)
                    {
                        listBoxItemContainer.ZIndex = 1;
                    }
                }

                if (isPastLastItem)
                {
                    hoveredItem = lastItem;
                }
                else if (isBeforeFirstItem)
                {
                    hoveredItem = firstItem;
                }

                if (hoveredItem is null)
                    return;

                if (hoveredItem != target)
                {
                    var adornerElement = hoveredItem;
                    var adornerLayer = AdornerLayer.GetAdornerLayer(adornerElement);

                    if (adornerLayer != null)
                    {
                        var adornedElement = new Border()
                        {
                            BorderThickness = isPastLastItem ? new Thickness(0, 0, 0, 4) : new Thickness(0, 4, 0, 0),
                            BorderBrush = new SolidColorBrush(Color.Parse("#ffc107"))
                        };
                        adornerLayer.Children.Add(adornedElement);
                        AdornerLayer.SetAdornedElement(adornedElement, adornerElement);
                    }
                }
            }
        }

        private void UpdateCursor(bool show)
        {
            if (_parent is { })
            {
                Window? window = _parent.GetVisualRoot() as Window;
                window.Cursor = show ? new Cursor(StandardCursorType.DragMove) : Cursor.Default;
            }
        }

        private void Parent_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            UpdateCursor(false);
            if (_parent is { })
            {
                var targetVisual = TargetControl ?? AssociatedObject;

                ContentPresenter targetItem = ControlExtension.FindAncestor<ContentPresenter>(targetVisual);
                targetItem.RenderTransform = new TranslateTransform();

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;
                _parent = null;
                isDragging = false;

                ItemsControl parentItemsControl = ControlExtension.FindAncestor<ItemsControl>(targetVisual);
                Point pos = e.GetPosition(parentItemsControl);

                // check if dragging outside left/right bounds
                if (pos.X < 0 || pos.X > parentItemsControl.Bounds.Width)
                {
                    return;
                }

                ItemsControl listBox = ControlExtension.FindAncestor<ItemsControl>(targetVisual);
                ContentPresenter hoveredItem = GetHoveredItem(parentItemsControl, pos, targetItem);

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom)
                    && pos.X > 0 && pos.X < listBox.Bounds.Width;

                ContentPresenter? firstItem = (ContentPresenter)listBox.GetLogicalChildren().MinBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Top);
                bool isBeforeFirstItem = (firstItem != null) && (isBeforeFirstItem = pos.Y < firstItem.Bounds.Top);

                targetItem.Opacity = 1;

                int SourceIndex = parentItemsControl.ItemContainerGenerator.IndexFromContainer(targetItem);
                int DestinationIndex;
                if (isPastLastItem)
                {
                    DestinationIndex = parentItemsControl.ItemCount - 1;
                }
                else if (isBeforeFirstItem)
                {
                    DestinationIndex = 0;
                }
                else
                {
                    DestinationIndex = parentItemsControl.ItemContainerGenerator.IndexFromContainer(hoveredItem);
                }

                for (int i = 0; i < parentItemsControl.ItemCount; i++)
                {
                    var listBoxItemContainer = parentItemsControl.ContainerFromIndex(i);

                    listBoxItemContainer.Classes.Remove("draggingover");
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItemContainer);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }

                    // deselect all entries if this is a drag operation.
                    // else, this is a single click operation so don't deselect all entries.
                    if (-1 != DestinationIndex)
                    {
                        //((ListBoxItem)listBoxItemContainer).IsSelected = false;
                    }
                }

                if (SourceIndex != DestinationIndex && DestinationIndex > -1)
                {
                    // Debug.Print($"Moved {SourceIndex} to {DestinationIndex}, isPastLastItemBounds: {isPastLastItemBounds}");

                    //MessageBus.Current.SendMessage(new MoveItemMessage()
                    //{
                    //    SourceIndex = SourceIndex,
                    //    DestinationIndex = DestinationIndex
                    //});

                    if (parentItemsControl.DataContext is SongEditorViewModel vm)
                    {
                        vm.song.Arrangement.Move(SourceIndex, DestinationIndex);
                    }
                }
            }
        }
    }
}