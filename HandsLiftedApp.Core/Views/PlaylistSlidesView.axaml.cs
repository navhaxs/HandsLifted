using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using Rect = Avalonia.Rect;

namespace HandsLiftedApp.Core.Views
{
    public partial class PlaylistSlidesView : UserControl
    {
        ItemsControl listBox;

        public PlaylistSlidesView()
        {
            InitializeComponent();

            listBox = this.FindControl<ItemsControl>("List");

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
            
                    var container = listBox.GetRealizedContainers().FirstOrDefault(container =>
                    {
                        ListBoxWithoutKey listBoxWithoutKey = container.FindDescendantOfType<ListBoxWithoutKey>();
                        if (listBoxWithoutKey != null)
                            return (listBoxWithoutKey.SelectedIndex > -1);
            
                        return false;
                    });
            
                    ListBoxWithoutKey listBoxWithoutKey = container.FindDescendantOfType<ListBoxWithoutKey>();
            
                    if (listBoxWithoutKey == null)
                        return;
            
                    if (listBoxWithoutKey.SelectedIndex > -1)
                    {
                        Control control =
                            listBoxWithoutKey.ItemContainerGenerator.ContainerFromIndex(listBoxWithoutKey
                                .SelectedIndex);
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            BringDescendantIntoView(control, new Rect(control.Bounds.Size));
                        });
                    }
                });

            MessageBus.Current.Listen<NavigateToItemMessage>()
                .Subscribe(x =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // layout pass required, see https://github.com/AvaloniaUI/Avalonia/issues/9992#issuecomment-1408205703
                        //Dispatcher.UIThread.RunJobs(DispatcherPriority.Layout);

                        // and now we can jump to view
                        // TODO: IF THE VIEW IS NOT ALREADY WITHIN VIEWPORT
                        var control = listBox.ItemContainerGenerator.ContainerFromIndex(x.Index);
                        if (control is not null)
                        {
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                // HACK: AddItemButton heiht = 56

                                if (control.Bounds.Top < scrollViewer.Offset.Y || control.Bounds.Top >
                                    (scrollViewer.Offset.Y + scrollViewer.Bounds.Height))
                                {
                                    scrollViewer.Offset = new Vector(0, control.Bounds.Top + 4);
                                    scrollViewer.ScrollToEnd();
                                    scrollViewer.Offset = new Vector(0, control.Bounds.Top + 4);
                                }

                                //Debug.Print($"NavigateToItemMessage={x.Index}, control.Bounds.Top={control.Bounds.Top}");
                            });
                        }
                    });
                });

            scrollViewer.GetObservable(Avalonia.Controls.ScrollViewer.OffsetProperty)
                .Subscribe(offset =>
                {
                    var lastIndex = 0;

                    if (offset.Y == scrollViewer.ScrollBarMaximum.Y)
                    {
                        lastIndex = listBox.ItemCount - 1;
                    }
                    else if (offset.Y > Double.Epsilon)
                    {
                        for (int i = 0; i < listBox.ItemCount; i++)
                        {
                            var c = listBox.ItemContainerGenerator.ContainerFromIndex(i);

                            if (c is null)
                                break;

                            if (c.Bounds.Top > Math.Round(offset.Y, 1))
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
            // var ctx = (Playlist<PlaylistStateImpl, ItemStateImpl>)this.DataContext;
            //
            // // todo dispose old one
            // ctx.WhenAnyValue(x => x.State.SelectedItemIndex)
            //     .ObserveOn(RxApp.MainThreadScheduler)
            //     .Select(x => { return x == -1; })
            //     .Subscribe(x =>
            //     {
            //         if (x)
            //         {
            //             scrollViewer.Offset = new Vector(0, 0);
            //         }
            //     });
        }

        private void PlaylistSlidesView_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.PageDown:
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    // MessageBus.Current.SendMessage(new FocusSelectedItem());

                    e.Handled = true;
                    break;
                case Key.PageUp:
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    // MessageBus.Current.SendMessage(new FocusSelectedItem());

                    e.Handled = true;
                    break;
            }
        }

        // <summary>
        /// Attempts to bring a portion of the target visual into view by scrolling the content.
        /// </summary>
        /// <param name="target">The target visual.</param>
        /// <param name="targetRect">The portion of the target visual to bring into view.</param>
        /// <returns>True if the scroll offset was changed; otherwise false.</returns>
        public bool BringDescendantIntoView(Visual target, Rect targetRect)
        {
            ScrollContentPresenter presenter = (ScrollContentPresenter)scrollViewer.Presenter;

            if (presenter.Child?.IsEffectivelyVisible != true)
            {
                return false;
            }

            var scrollable = presenter.Child as ILogicalScrollable;
            var control = target as Control;

            if (scrollable?.IsLogicalScrollEnabled == true && control != null)
            {
                return scrollable.BringIntoView(control, targetRect);
            }

            var transform = target.TransformToVisual(presenter.Child);

            if (transform == null)
            {
                return false;
            }

            ContentPresenter controlParent = ControlExtension.FindAncestor<ContentPresenter>(control);

            var rect = targetRect.TransformToAABB(transform.Value);
            var offset = presenter.Offset;
            var result = false;

            var Ypadding = target.Bounds.Height * 1.1;

            if (rect.Bottom + Ypadding > offset.Y + presenter.Viewport.Height)
            {
                //if (Ypadding < scrollViewer.Bounds.Height)
                //{
                //    offset = offset.WithY(controlParent.Bounds.Y);
                //}
                //else
                //{
                offset = offset.WithY((rect.Bottom + Ypadding - presenter.Viewport.Height) +
                                      presenter.Child.Margin.Top);
                //}
                result = true;
            }

            if (rect.Y - Ypadding < offset.Y)
            {
                //if (Ypadding < scrollViewer.Bounds.Height)
                //{
                //    offset = offset.WithY(controlParent.Bounds.Y);
                //}
                //else
                //{
                offset = offset.WithY(rect.Y - Ypadding);
                //}
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
                // BUG: when this is executed, using the mouse to drag the scrollbar stops working!
                //presenter.Offset = offset;
                scrollViewer.Offset = offset;
            }

            return result;
        }
    }
}