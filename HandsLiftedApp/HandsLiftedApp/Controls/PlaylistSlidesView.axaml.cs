using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive.Linq;

namespace HandsLiftedApp.Controls
{
    public partial class PlaylistSlidesView : UserControl
    {
        ListBox listBox;

        public PlaylistSlidesView()
        {
            InitializeComponent();

            listBox = this.FindControl<ListBox>("List");


            MessageBus.Current.Listen<NavigateToItemMessage>()
               .Subscribe(x =>
               {
                   var control = listBox.ItemContainerGenerator.ContainerFromIndex(x.Index);

                   if (control is not null)
                   {
                       listBox.Scroll.Offset = new Vector(0, control.Bounds.Top);

                   }
               });

            listBox.GetObservable(ListBox.ScrollProperty)
                .OfType<ScrollViewer>()
                .Take(1)
                .Subscribe(sv => {
                    var x = sv;


                    sv.GetObservable(ScrollViewer.OffsetProperty)
                        .Subscribe(offset =>
                        {
                            Debug.Print(offset.ToString());
                            if (offset.Y <= Double.Epsilon)
                            {
                                Console.WriteLine("At Top");
                            }

                            var lastIndex = 0;
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

                            Debug.Print(lastIndex.ToString());
                            
                            MessageBus.Current.SendMessage(new SpyScrollUpdateMessage() { Index = lastIndex });


                            //var delta = Math.Abs(_verticalHeightMax - offset.Y);
                            //if (delta <= Double.Epsilon)
                            //{
                            //    Console.WriteLine("At Bottom");
                            //    var vm = DataContext as MainWindowViewModel;
                            //    vm?.AddItems();
                            //}
                        });
                        //sv.GetObservable(ScrollViewer.VerticalScrollBarMaximumProperty)
                        //    .Subscribe(newMax => _verticalHeightMax = newMax)
                        //    .DisposeWith(_scrollViewerDisposables);
                        });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
