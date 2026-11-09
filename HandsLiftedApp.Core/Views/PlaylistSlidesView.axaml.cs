using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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
                            listBoxWithoutKey.ContainerFromIndex(listBoxWithoutKey
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
                        var control = listBox.ContainerFromIndex(x.Index);
                        if (control is not null)
                        {
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var GUTTER = 300;
                                
                                // flash starts together with the scroll; its fade is long enough to outlast the spring
                                if (x.Flash)
                                    control.FindDescendantOfType<ItemSlidesView>()?.Flash();

                                if (control.Bounds.Top < scrollViewer.Offset.Y + GUTTER || control.Bounds.Top >
                                    (scrollViewer.Offset.Y + scrollViewer.Bounds.Height - GUTTER))
                                {
                                    AnimateScrollTo(control.Bounds.Top + 4);
                                }

                                //Debug.Print($"NavigateToItemMessage={x.Index}, control.Bounds.Top={control.Bounds.Top}");
                            });
                        }
                    });
                });

            // user input takes over from an in-flight animated scroll
            scrollViewer.AddHandler(PointerWheelChangedEvent, (_, _) => CancelScrollAnimation(),
                RoutingStrategies.Tunnel);
            scrollViewer.AddHandler(PointerPressedEvent, (_, _) => CancelScrollAnimation(),
                RoutingStrategies.Tunnel);

            scrollViewer.GetObservable(Avalonia.Controls.ScrollViewer.OffsetProperty)
                .Subscribe(offset =>
                {
                    // the outline already shows the clicked item; don't let spy-scroll flick its selection
                    // through every item we pass on the way. The final frame clears the flag first, so one
                    // update is still sent on arrival.
                    if (_isScrollAnimating)
                        return;

                    var lastIndex = 0;

                    if (offset.Y == scrollViewer.ScrollBarMaximum.Y)
                    {
                        lastIndex = listBox.ItemCount - 1;
                    }
                    else if (offset.Y > Double.Epsilon)
                    {
                        for (int i = 0; i < listBox.ItemCount; i++)
                        {
                            var c = listBox.ContainerFromIndex(i);

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

        // ---- animated scroll: critically damped spring ----

        /// <summary>
        /// Spring stiffness (natural frequency, rad/s). Higher = snappier. Settling time is roughly 6/ω
        /// regardless of distance (14 => ~0.4s).
        /// </summary>
        private const double ScrollSpringOmega = 14.0;

        // Incremented to cancel the in-flight frame loop (each frame callback checks the id it started with).
        private int _scrollAnimationId;
        private bool _isScrollAnimating;
        private double _scrollTargetY;
        private double _scrollVelocity; // px/s, kept across retargets so redirecting mid-scroll stays smooth

        private void CancelScrollAnimation()
        {
            _scrollAnimationId++;
            _isScrollAnimating = false;
            _scrollVelocity = 0;
        }

        /// <summary>
        /// Springs the playlist scroll position to <paramref name="targetY"/>. If a scroll is already in flight it is
        /// retargeted and keeps its current velocity (no restart). Wheel/click input cancels it.
        /// </summary>
        private void AnimateScrollTo(double targetY)
        {
            _scrollTargetY = Math.Clamp(targetY, 0, Math.Max(0, scrollViewer.ScrollBarMaximum.Y));

            if (_isScrollAnimating)
                return; // running loop picks up the new target

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null || Math.Abs(_scrollTargetY - scrollViewer.Offset.Y) < 1)
            {
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, _scrollTargetY);
                return;
            }

            var id = ++_scrollAnimationId;
            _isScrollAnimating = true;
            _scrollVelocity = 0;
            TimeSpan? last = null;

            void Frame(TimeSpan now)
            {
                if (id != _scrollAnimationId)
                    return; // cancelled

                // cap dt so a stalled frame can't make a huge jump
                var dt = last is null ? 1.0 / 60 : Math.Min((now - last.Value).TotalSeconds, 1.0 / 20);
                last = now;

                // Exact analytic step of a critically damped spring (stable for any dt):
                //   x(t) = (x0 + (v0 + w*x0)*t) * e^(-w*t)
                //   v(t) = (v0 - w*(v0 + w*x0)*t) * e^(-w*t)
                var w = ScrollSpringOmega;
                var x0 = scrollViewer.Offset.Y - _scrollTargetY;
                var v0 = _scrollVelocity;
                var decay = Math.Exp(-w * dt);
                var c = v0 + w * x0;
                var x = (x0 + c * dt) * decay;
                var v = (v0 - w * c * dt) * decay;

                if (Math.Abs(x) < 0.5 && Math.Abs(v) < 5)
                {
                    // settled. Clear the flag first so the final Offset change still reaches the spy-scroll subscription
                    _isScrollAnimating = false;
                    _scrollVelocity = 0;
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, _scrollTargetY);
                    return;
                }

                _scrollVelocity = v;
                var maxY = Math.Max(0, scrollViewer.ScrollBarMaximum.Y);
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, Math.Clamp(_scrollTargetY + x, 0, maxY));
                topLevel.RequestAnimationFrame(Frame);
            }

            topLevel.RequestAnimationFrame(Frame);
        }

        private void PlaylistSlidesView_DataContextChanged(object? sender, EventArgs e)
        {
            // var ctx = (Playlist<PlaylistStateImpl, ItemStateImpl>)this.DataContext;
            //
            // // todo dispose old one
            // ctx.WhenAnyValue(x => x.State.SelectedItemIndex)
            //     .ObserveOn(RxSchedulers.MainThreadScheduler)
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
            
            //
            // switch (e.Key)
            // {
            //     case Key.PageDown:
            //     case Key.Right:
            //     case Key.Space:
            //         MessageBus.Current.SendMessage(new ActionMessage()
            //             { Action = ActionMessage.NavigateSlideAction.NextSlide });
            //         MessageBus.Current.SendMessage(new FocusSelectedItem());
            //
            //         e.Handled = true;
            //         break;
            //     case Key.PageUp:
            //     case Key.Left:
            //         MessageBus.Current.SendMessage(new ActionMessage()
            //             { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
            //         MessageBus.Current.SendMessage(new FocusSelectedItem());
            //
            //         e.Handled = true;
            //         break;
            // }
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