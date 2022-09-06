using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive.Linq;

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

            MessageBus.Current.Listen<NavigateToItemMessage>()
               .Subscribe(x =>
               {
                   var control = listBox.ItemContainerGenerator.ContainerFromIndex(x.Index);

                   if (control is not null)
                   {
                       scrollViewer.Offset = new Vector(0, control.Bounds.Top);
                       Debug.Print($"NavigateToItemMessage={x.Index}, control.Bounds.Top={control.Bounds.Top}");
                   }
               });

            scrollViewer.GetObservable(ScrollViewer.OffsetProperty)
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
            ctx.WhenAnyValue(x => x.State.SelectedIndex)
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
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    break;
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    break;
            }
            e.Handled = true;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
