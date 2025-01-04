using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Behaviours;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Controls.Behaviours
{
    /// <summary>
    /// A behavior that allows controls to be moved around the canvas using RenderTransform of <see cref="Behavior.AssociatedObject"/>.
    /// </summary>
    public sealed class SlideThumbnailBehavior : Behavior<Control>
    {
        /// <summary>
        /// Identifies the <seealso cref="TargetControl"/> avalonia property.
        /// </summary>
        public static readonly StyledProperty<Control?> TargetControlProperty =
            AvaloniaProperty.Register<DragControlBehavior, Control?>(nameof(TargetControl));

        private Control? _parent;
        private Point? _pointerPressedInitialPoint;
        private int _insertIndex;

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
                source.PointerMoved += Source_PointerMoved;
                source.PointerReleased += Source_PointerReleased;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            var source = AssociatedObject;
            if (source is { })
            {
                source.PointerPressed -= Source_PointerPressed;
                source.PointerMoved -= Source_PointerMoved;
                source.PointerReleased += Source_PointerReleased;

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
                _pointerPressedInitialPoint = e.GetPosition(_parent);
                
                // prevent default ListBox behaviour which would update the SelectedItemIndex (due to data binding) on click event, we want to control slide navigation ourselves - and NOT do anything if this becomes a drag event
                e.Handled = true;
            }
        }    
        
        private void Source_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { } && sender is Control parent)
            {
                _pointerPressedInitialPoint = null;
                
                var parentListBox = ControlExtension.FindAncestor<ListBoxWithoutKey>(parent);
                var parentListBoxItem = ControlExtension.FindAncestor<ListBoxItem>(parent);
                var sourceListBoxIndex = -1;
                for (var idx = 0; idx < parentListBox.Items.Count; idx++)
                {
                    if (parentListBox.ContainerFromIndex(idx) == parentListBoxItem) 
                    {
                        sourceListBoxIndex = idx;
                        break;
                    }
                }

                if (parent.DataContext is Slide slide && sourceListBoxIndex > -1)
                {
                    if (parentListBox.DataContext is Item item)
                    {
                        var slideReference = new SlideReference()
                            { ItemUUID = item.UUID, SlideIndex = sourceListBoxIndex };
                        Log.Information($"OnSlideClickCommand [{slideReference}]");

                        MessageBus.Current.SendMessage(new NavigateToSlideReferenceAction() { SlideReference = slideReference });
                    }
                }
            }
        }

        private bool _isDragging = false;
        
        private void Source_PointerMoved(object? sender, PointerEventArgs e)
        {
            var target = TargetControl ?? AssociatedObject;
            if (target is { })
            {
                if (!_isDragging && _pointerPressedInitialPoint != null)
                {
                    Point pos = e.GetPosition(_parent);
                    if (Math.Abs(pos.Y - _pointerPressedInitialPoint.Value.Y) > 6 || Math.Abs(pos.X - _pointerPressedInitialPoint.Value.X) > 6) // deadzone
                    {
                        StartDrag(target, e);
                    }
                }
            }
        }

        private async void StartDrag(Control parent, PointerEventArgs e)
        {
            _isDragging = true;

            var dragData = new DataObject();
            var topLevel = TopLevel.GetTopLevel(parent);
            
            var parentListBox = ControlExtension.FindAncestor<ListBoxWithoutKey>(parent);
            var parentListBoxItem = ControlExtension.FindAncestor<ListBoxItem>(parent);
            var sourceListBoxIndex = -1;
            for (var idx = 0; idx < parentListBox.Items.Count; idx++)
            {
                if (parentListBox.ContainerFromIndex(idx) == parentListBoxItem) 
                {
                    sourceListBoxIndex = idx;
                    break;
                }
            }

            if (parent.DataContext is Slide && sourceListBoxIndex > -1)
            {
                if (parentListBox.DataContext is Item sourceItem)
                {
                    dragData.Set(SlideDragDropCustomDataFormat.CustomFormat,
                        new SlideDragDropCustomDataFormat() { SourceItemUUID = sourceItem.UUID, SourceSlideIndex = sourceListBoxIndex });
                    
                    var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
                }
            }
            
            _isDragging = false;
            _pointerPressedInitialPoint = null;
        }
    }
    
}