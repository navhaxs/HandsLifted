using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Linq;

namespace HandsLiftedApp.Behaviours
{
    /// <summary>
    /// A behavior that allows controls to be moved around the canvas using RenderTransform of <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    public sealed class DragControlBehavior : Behavior<Control>
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
                _parent = (Control)target.GetLogicalParent();

                if (!(target.RenderTransform is TranslateTransform))
                {
                    target.RenderTransform = new TranslateTransform();
                }

                _previous = e.GetPosition(_parent);
                if (_parent != null)
                {
                    _parent.PointerMoved += Parent_PointerMoved;
                    _parent.PointerReleased += Parent_PointerReleased;
                }

                ListBox listBox = (ListBox)target.Parent;
                var SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(target);
            }
        }

        private ListBoxItem GetHoveredItem(ListBox listBox, Point pos, Control? target)
        {
            return (ListBoxItem)listBox.GetLogicalChildren()
               .Where(listBoxItem => listBoxItem != target)
               .FirstOrDefault(x => listBox.GetVisualsAt(pos)
                   .Contains(
                       ((Visual)x).GetVisualChildren().First())
               );
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
                ListBox listBox = (ListBox)target.Parent;
                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);

                    listBoxItemContainer.Classes.Remove("draggingover");
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
            //args.Properties;
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

                if (target.RenderTransform is TranslateTransform tr)
                {
                    //tr.X += pos.X - _previous.X;
                    tr.Y += pos.Y - _previous.Y;
                }
                _previous = pos;

                ListBox listBox = (ListBox)target.Parent;
                ListBoxItem hoveredItem = GetHoveredItem(listBox, pos, target);

                // check if dragging past the last item
                ListBoxItem? lastItem = (ListBoxItem)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ListBoxItem)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom)
                    && pos.X > 0 && pos.X < listBox.Bounds.Width;

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItemContainer);
                    adornerLayer.Children.Clear();
                    listBoxItemContainer.ZIndex = 5;
                }

                if (isPastLastItem)
                {
                    hoveredItem = lastItem;
                }

                if (hoveredItem is null)
                    return;

                if (hoveredItem != target)
                {
                    hoveredItem.ZIndex = 1;
                    var adornerElement = hoveredItem;
                    var adornerLayer = AdornerLayer.GetAdornerLayer(adornerElement);

                    if (adornerLayer != null)
                    {
                        var adornedElement = new Border()
                        {
                            CornerRadius = new CornerRadius(3, 0, 0, 3),
                            BorderThickness = new Thickness(2, 2, 0, 2),
                            BorderBrush = new SolidColorBrush(Color.Parse("#9a93cd"))
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
                var target = TargetControl ?? AssociatedObject;
                target.RenderTransform = new TranslateTransform();

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;
                _parent = null;
                isDragging = false;

                ListBox listBox = (ListBox)target.Parent;
                Point pos = e.GetPosition(listBox);


                // check if dragging outside left/right bounds
                if (pos.X < 0 || pos.X > listBox.Bounds.Width)
                {
                    return;
                }

                ListBoxItem hoveredItem = GetHoveredItem(listBox, pos, target);

                // check if dragging past the last item
                ListBoxItem? lastItem = (ListBoxItem)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ListBoxItem)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom);

                int SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(target);
                int DestinationIndex = isPastLastItem ? listBox.ItemCount - 1 : listBox.ItemContainerGenerator.IndexFromContainer(hoveredItem);

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);

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
                        ((ListBoxItem)listBoxItemContainer).IsSelected = false;
                    }
                }

                if (SourceIndex != DestinationIndex && DestinationIndex > -1)
                {
                    //Debug.Print($"Moved {SourceIndex} to {DestinationIndex}, isPastLastItemBounds: {isPastLastItemBounds}");


                    MessageBus.Current.SendMessage(new MoveItemMessage()
                    {
                        SourceIndex = SourceIndex,
                        DestinationIndex = DestinationIndex
                    });
                }
            }
        }
    }
}