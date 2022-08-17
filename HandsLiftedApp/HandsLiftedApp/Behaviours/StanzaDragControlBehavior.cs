using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.ViewModels.Editor;
using ReactiveUI;
using System.Diagnostics;
using System.Linq;

namespace HandsLiftedApp.Behaviours
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

                ItemsControl listBox = (ItemsControl)target.Parent.Parent;
                var SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(target.Parent);
            }
        }

        private ContentPresenter GetHoveredItem(ItemsControl listBox, Point pos, IControl? target)
        {
            return (ContentPresenter)listBox.GetLogicalChildren()
               .Where(listBoxItem => listBoxItem != target)
               .FirstOrDefault(x => listBox.GetVisualsAt(pos)
                   .Contains(
                       ((IVisual)x).GetVisualChildren().First())
               );
        }

        private void Parent_PointerMoved(object? sender, PointerEventArgs args)
        {
            var target = TargetControl ?? AssociatedObject;
            //args.Properties;
            if (args.InputModifiers.HasFlag(InputModifiers.RightMouseButton))
            {
                UpdateCursor(false);
                target.RenderTransform = new TranslateTransform();

                _parent.PointerMoved -= Parent_PointerMoved;
                _parent.PointerReleased -= Parent_PointerReleased;
                _parent = null;

                ItemsControl listBox = (ItemsControl)target.Parent.Parent;
                foreach (var listBoxItem in listBox.ItemContainerGenerator.Containers)
                {
                    listBoxItem.ContainerControl.Classes.Remove("draggingover");
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItem.ContainerControl);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }
                }

                return;
            }

            UpdateCursor(true);
            if (target is { })
            {
                Point pos = args.GetPosition(_parent.Parent);
                Point pos1 = args.GetPosition(_parent);
                if (target.RenderTransform is TranslateTransform tr)
                {
                    tr.X += pos1.X - _previous.X;
                    //tr.Y += pos.Y - _previous.Y;
                }
                _previous = pos1;

                ItemsControl listBox = (ItemsControl)target.Parent.Parent;
                ContentPresenter hoveredItem = GetHoveredItem(listBox, pos, target.Parent);

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = false;// (lastItem != null) && (isPastLastItem = pos1.Y > lastItem.Bounds.Bottom);

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

        private void UpdateCursor(bool show)
        {
            if (_parent is { })
            {
                Window? window = _parent.VisualRoot as Window;
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

                ItemsControl listBox = (ItemsControl)target.Parent.Parent;
                Point pos = e.GetPosition(listBox);
                ContentPresenter hoveredItem = GetHoveredItem(listBox, pos, target.Parent);

                // check if dragging past the last item
                ContentPresenter? lastItem = (ContentPresenter)listBox.GetLogicalChildren().MaxBy(listBoxItem => ((ContentPresenter)listBoxItem).Bounds.Bottom);
                bool isPastLastItem = false; // (lastItem != null) && (isPastLastItem = pos.Y > lastItem.Bounds.Bottom);

                int SourceIndex = listBox.ItemContainerGenerator.IndexFromContainer(target.Parent);
                int DestinationIndex = isPastLastItem ? listBox.ItemCount - 1 : listBox.ItemContainerGenerator.IndexFromContainer(hoveredItem);

                foreach (var listBoxItem in listBox.ItemContainerGenerator.Containers)
                {
                    listBoxItem.ContainerControl.Classes.Remove("draggingover");
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBoxItem.ContainerControl);

                    if (adornerLayer != null)
                    {
                        adornerLayer.Children.Clear();
                    }
                }

                if (SourceIndex != DestinationIndex && DestinationIndex > -1)
                {
                    Debug.Print($"Moved {SourceIndex} to {DestinationIndex}, isPastLastItem: {isPastLastItem}");
                    SongEditorViewModel ctx = (SongEditorViewModel)listBox.DataContext;
                    ctx.song.Arrangement.Move(SourceIndex, DestinationIndex);
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