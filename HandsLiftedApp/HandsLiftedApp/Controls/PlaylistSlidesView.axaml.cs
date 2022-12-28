using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using Rect = Avalonia.Rect;

namespace HandsLiftedApp.Controls
{
    public partial class PlaylistSlidesView : UserControl
    {
        ItemsControl listBox;
        ScrollViewer scrollViewer;


        public PlaylistSlidesView()
        {
            InitializeComponent();

            listBox = this.FindControl<ItemsControl>("List");
            scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");

            this.DataContextChanged += PlaylistSlidesView_DataContextChanged;


            this.AddHandler(RequestBringIntoViewEvent, (s, e) =>
            {
                //TODO write custom scroll handler
                //which takes into account the whole item container (not just slide within the item's listbox)
                //scrollViewer.Offset = new Vector(0, 0);
                //e.Handled = true;
            });

            MessageBus.Current.Listen<FocusSelectedItem>()
               .Subscribe(x =>
               {

                   // TODO add a flag so this can be conditionally skipped
                   // e.g. user can disable "auto follow mode" if they are busy editing elsewhere in the playlist

                   Avalonia.Controls.Generators.ItemContainerInfo itemContainerInfo = listBox.ItemContainerGenerator.Containers.FirstOrDefault(container =>
                    {
                        var xb = container.ContainerControl;
                        ListBoxWithoutKey listBoxWithoutKey = xb.FindDescendantOfType<ListBoxWithoutKey>();
                        return (listBoxWithoutKey.SelectedIndex > -1);
                    });

                   if (itemContainerInfo == null)
                   {
                       return;
                   }

                   var xb = itemContainerInfo.ContainerControl;

                   ListBoxWithoutKey listBoxWithoutKey = xb.FindDescendantOfType<ListBoxWithoutKey>();

                   if (listBoxWithoutKey.SelectedIndex > -1)
                   {
                       IControl control = listBoxWithoutKey.ItemContainerGenerator.ContainerFromIndex(listBoxWithoutKey.SelectedIndex);
                       BringDescendantIntoView(control, new Rect(control.Bounds.Size));
                   }
               });

            MessageBus.Current.Listen<NavigateToItemMessage>()
               .Subscribe(x =>
               {
                   var control = listBox.ItemContainerGenerator.ContainerFromIndex(x.Index);

                   if (control is not null)
                   {
                       scrollViewer.Offset = new Vector(0, control.Bounds.Top);
                       //Debug.Print($"NavigateToItemMessage={x.Index}, control.Bounds.Top={control.Bounds.Top}");
                   }
               });

            scrollViewer.GetObservable(Avalonia.Controls.ScrollViewer.OffsetProperty)
                .Subscribe(offset =>
                {
                    var lastIndex = 0;

                    if (offset.Y > Double.Epsilon)
                    {
                        for (int i = 0; i < listBox.ItemCount; i++)
                        {
                            var c = listBox.ItemContainerGenerator.ContainerFromIndex(i);

                            if (c is null)
                                break;

                            if (c.Bounds.Top > offset.Y)
                            {
                                break;
                            }

                            lastIndex = i;
                        }
                    }

                    MessageBus.Current.SendMessage(new SpyScrollUpdateMessage() { Index = lastIndex });
                });

            this.KeyDown += PlaylistSlidesView_KeyDown;
        }

        private void PlaylistSlidesView_DataContextChanged(object? sender, EventArgs e)
        {
            var ctx = (Playlist<PlaylistStateImpl, ItemStateImpl>)this.DataContext;

            // todo dispose old one
            ctx.WhenAnyValue(x => x.State.SelectedItemIndex)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(x =>
                {
                    return x == -1;
                })
                .Subscribe(x =>
                {
                    if (x)
                    {
                        scrollViewer.Offset = new Vector(0, 0);
                    }
                });
        }

        private void PlaylistSlidesView_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.PageDown:
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());

                    e.Handled = true;
                    break;
                case Key.PageUp:
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());

                    e.Handled = true;
                    break;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // <summary>
        /// Attempts to bring a portion of the target visual into view by scrolling the content.
        /// </summary>
        /// <param name="target">The target visual.</param>
        /// <param name="targetRect">The portion of the target visual to bring into view.</param>
        /// <returns>True if the scroll offset was changed; otherwise false.</returns>
        public bool BringDescendantIntoView(IVisual target, Rect targetRect)
        {
            ScrollContentPresenter presenter = (ScrollContentPresenter)scrollViewer.Presenter;

            if (presenter.Child?.IsEffectivelyVisible != true)
            {
                return false;
            }

            var scrollable = presenter.Child as ILogicalScrollable;
            var control = target as IControl;

            if (scrollable?.IsLogicalScrollEnabled == true && control != null)
            {
                return scrollable.BringIntoView(control, targetRect);
            }

            var transform = target.TransformToVisual(presenter.Child);

            if (transform == null)
            {
                return false;
            }

            var rect = targetRect.TransformToAABB(transform.Value);
            var offset = presenter.Offset;
            var result = false;

            var Ypadding = target.Bounds.Height * 1.1;

            if (rect.Bottom + Ypadding > offset.Y + presenter.Viewport.Height)
            {
                offset = offset.WithY((rect.Bottom + Ypadding - presenter.Viewport.Height) + presenter.Child.Margin.Top);
                result = true;
            }

            if (rect.Y - Ypadding < offset.Y)
            {
                offset = offset.WithY(rect.Y - Ypadding);
                result = true;
            }

            if (rect.Right > offset.X + presenter.Viewport.Width)
            {
                offset = offset.WithX((rect.Right - presenter.Viewport.Width) + presenter.Child.Margin.Left);
                result = true;
            }

            if (rect.X < offset.X)
            {
                offset = offset.WithX(rect.X);
                result = true;
            }

            if (result)
            {
                presenter.Offset = offset;
            }

            return result;
        }
    }
}
