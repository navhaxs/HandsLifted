using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
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

            MessageBus.Current.Listen<NavigateToItemMessage>()
               .Subscribe(x =>
               {
                   var control = listBox.ItemContainerGenerator.ContainerFromIndex(x.Index);

                   if (control is not null)
                   {
                       scrollViewer.Offset = new Vector(0, control.Bounds.Top);

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

        private void PlaylistSlidesView_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new NavigateSlideMessage() { Action = NavigateSlideMessage.NavigateSlideAction.NextSlide });
                    break;
                case Key.Left:
                    MessageBus.Current.SendMessage(new NavigateSlideMessage() { Action = NavigateSlideMessage.NavigateSlideAction.PreviousSlide });
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
