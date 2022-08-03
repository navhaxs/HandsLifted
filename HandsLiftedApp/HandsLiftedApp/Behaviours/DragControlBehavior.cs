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

        private IControl? _parent;
        private Point _previous;

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

        private void Source_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                _parent = target.Parent;

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
                       ((IVisual)x).GetVisualChildren().First())
               );
        }

        private void Parent_PointerMoved(object? sender, PointerEventArgs args)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                Point pos = args.GetPosition(_parent);
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
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom);

                foreach (var listBoxItem in listBox.ItemContainerGenerator.Containers)
                {
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItem.ContainerControl);
                    adornerLayer.Children.Clear();
                    listBoxItem.ContainerControl.ZIndex = 5;
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

        private void Parent_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_parent is { })
            {
                var target = TargetControl ?? AssociatedObject;
                target.RenderTransform = new TranslateTransform();

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;
                _parent = null;

                ListBox listBox = (ListBox)target.Parent;
                Point pos = e.GetPosition(listBox);
                ListBoxItem hoveredItem = GetHoveredItem(listBox, pos, target);

                // check if dragging past the last item
                ListBoxItem? lastItem = (ListBoxItem)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ListBoxItem)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom);

                int SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(target);
                int DestinationIndex = isPastLastItem ? listBox.ItemCount - 1 : listBox.ItemContainerGenerator.IndexFromContainer(hoveredItem);

                foreach (var listBoxItem in listBox.ItemContainerGenerator.Containers)
                {
                    listBoxItem.ContainerControl.Classes.Remove("draggingover");
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItem.ContainerControl);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }

                    // deselect all entries if this is a drag operation.
                    // else, this is a single click operation so don't deselect all entries.
                    if (-1 != DestinationIndex)
                    {
                        ((ListBoxItem)listBoxItem.ContainerControl).IsSelected = false;
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