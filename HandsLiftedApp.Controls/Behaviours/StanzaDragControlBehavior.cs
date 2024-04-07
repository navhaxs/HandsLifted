using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Behaviours;
using HandsLiftedApp.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

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

                    Data.Models.Items.SongItem ctx = (Data.Models.Items.SongItem)listBox.DataContext;
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

                    target.Opacity = 0.6;
                    
                    var m = _parent.GetVisualRoot();
                    if (m is Window)
                    {
                        (m as Window).LostFocus += StanzaDragControlBehavior_LostFocus;
                        (m as Window).PointerPressed += StanzaDragControlBehavior_PointerPressed;
                    }
                }
            }
        }

        private void StanzaDragControlBehavior_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsRightButtonPressed)
            {
                ResetDraggingState();
            }
        }

        private void StanzaDragControlBehavior_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ResetDraggingState();
        }

        private ContentPresenter GetHoveredItem(ItemsControl listBox, Point pos, Control? target)
        {
            var x = (ContentPresenter)listBox.GetLogicalChildren()
               .Where(listBoxItem => listBoxItem != target)
               .FirstOrDefault(x =>
               {
                   var listItemVisuals = ((Visual)x).GetVisualChildren(); // Grid
                   IEnumerable<Visual> targetPosVisuals = listBox.GetVisualsAt(pos); // children

                   if (targetPosVisuals.Count() > 0)
                   {
                       return listItemVisuals.Any(source =>
                       {
                           var mm = source.GetVisualDescendants();
                           return mm.Any(targetDecendant => targetPosVisuals.Contains(targetDecendant));
                       });

                   }
                   return false;
               });

            return x;
        }

        bool match(Visual target, Visual source)
        {
            if (target == source)
                return true;

            return target.GetVisualDescendants().Any(targetDescendant => match(targetDescendant, source));
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
                
                ItemsControl listBox = GetParent(target);

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

                return;
            }

            UpdateCursor(new Cursor(StandardCursorType.DragMove));

            if (target is { })
            {
                ItemsControl listBox = GetParent(target);

                Point pos = args.GetPosition((Control)_parent.Parent);
                Point pos1 = args.GetPosition(_parent);
                if (target.RenderTransform is TranslateTransform tr)
                {
                    tr.X += pos1.X - _previous.X;
                    //tr.Y += pos1.Y - _previous.Y; % target.Height;
                    // TODO FIXME for multiple rows where negative Y may actually be appropriate
                    //tr.Y = Math.Clamp(tr.Y + pos1.Y - _previous.Y, 0, listBox.Bounds.Bottom - target.Bounds.Height);
                }
                _previous = pos1;

                ContentPresenter hoveredItem = GetHoveredItem(listBox, pos, GetItem(target));

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);

                // TODO
                bool isPastLastItem = false;// (lastItem != null) && (isPastLastItem = pos1.Y > lastItem.Bounds.Bottom);

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
                            //CornerRadius = new CornerRadius(3, 0, 0, 3),
                            BorderThickness = new Thickness(4, 0, 0, 0),
                            BorderBrush = new SolidColorBrush(Color.Parse("#FF4B31"))
                        };
                        adornerLayer.Children.Add(adornedElement);
                        AdornerLayer.SetAdornedElement(adornedElement, adornerElement);
                    }
                }
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

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;

                UpdateCursor();

                _parent = null;
            }
        }

        private void Parent_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_parent is { })
            {
                var target = TargetControl ?? AssociatedObject;

                ItemsControl listBox = GetParent(target);
                Point pos = e.GetPosition(listBox);
                ContentPresenter hoveredItem = GetHoveredItem(listBox, pos, GetItem(target));

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = false; // (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom);

                int SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(((Control)target).FindAncestorOfType<ContentPresenter>());
                int DestinationIndex = isPastLastItem ? listBox.ItemCount - 1 : listBox.ItemContainerGenerator.IndexFromContainer(hoveredItem);

                for (int i = 0; i < listBox.ItemCount; i++)
                {
                    var listBoxItemContainer = listBox.ContainerFromIndex(i);
                    listBoxItemContainer.Classes.Remove("draggingover");
                    listBoxItemContainer.FindDescendantOfType<DockPanel>().Opacity = 1;
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItemContainer);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }
                }

                ResetDraggingState();
                if (SourceIndex != DestinationIndex
                    && SourceIndex > -1
                    && DestinationIndex > -1)
                {
                    //Debug.Print($"Moved {SourceIndex} to {DestinationIndex}, isPastLastItem: {isPastLastItem}");
                    Data.Models.Items.SongItem ctx = (Data.Models.Items.SongItem)listBox.DataContext;

                    try
                    {
                        ctx.Arrangement.Move(SourceIndex, DestinationIndex);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Failed to move item", ex);
                    }

                    //ctx.Slides.ElementAt(0)
                    //var ctx = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>)target.DataContext;
                    //SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> a = ctx.Value;
                    //MessageBus.Current.SendMessage(new MoveItemMessage()
                    //{
                    //    SourceIndex = SourceIndex,
                    //    DestinationIndex = DestinationIndex
                    //});
                }
            }
        }
    }
}