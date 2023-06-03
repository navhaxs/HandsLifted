using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;

namespace HandsLiftedApp.Controls
{
    // TODO disable spyscroll feature as its rather buggy
    // good effort tho.
    public partial class ItemOrderListView : UserControl
    {
        private static readonly object syncSlidesLock = new object();

        ListBox listBox;
        
        public ItemOrderListView()
        {
            InitializeComponent();

            listBox = this.FindControl<ListBox>("itemsListBox");
            listBox.SelectionChanged += ListBox_SelectionChanged;

            MessageBus.Current.Listen<SpyScrollUpdateMessage>()
               .Subscribe(x =>
               {
                   lock (syncSlidesLock)
                   {
                       listBox.SelectionChanged -= ListBox_SelectionChanged;
                       //listBox.SelectedIndex = x.Index;
                       listBox.SelectionChanged += ListBox_SelectionChanged;
                   }
               });


            //SetupDnd("Text", d => d.Set(DataFormats.Text, $"Text was dragged"), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

            //SetupDnd("Custom", d => d.Set(CustomFormat, "Test123"), DragDropEffects.Move);
            SetupDnd("Files", d => d.Set(DataFormats.FileNames, new[] { Assembly.GetEntryAssembly()?.GetModules().FirstOrDefault()?.FullyQualifiedName }), DragDropEffects.Copy);
        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (listBox.SelectedIndex > -1)
                MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = listBox.SelectedIndex });

        }

        Control lastAdornerElement;

        void SetupDnd(string suffix, Action<DataObject> factory, DragDropEffects effects)
        {
            //var dragMe = this.Get<Border>("DragMe" + suffix);
            //var dragState = this.Get<TextBlock>("DragState" + suffix);

            async void DoDrag(object? sender, Avalonia.Input.PointerPressedEventArgs e)
            {
                var dragData = new DataObject();
                factory(dragData);

                //var result = await DragDrop.DoDragDrop(e, dragData, effects);
                //switch (result)
                //{
                //    case DragDropEffects.Move:
                //        dragState.Text = "Data was moved";
                //        break;
                //    case DragDropEffects.Copy:
                //        dragState.Text = "Data was copied";
                //        break;
                //    case DragDropEffects.Link:
                //        dragState.Text = "Data was linked";
                //        break;
                //    case DragDropEffects.None:
                //        dragState.Text = "The drag operation was canceled";
                //        break;
                //    default:
                //        dragState.Text = "Unknown result";
                //        break;
                //}
            }

            void DragOver(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                // Only allow if the dragged data contains text or filenames.
                if (!e.Data.Contains(DataFormats.Text)
                    && !e.Data.Contains(DataFormats.FileNames)
                    )
                    //&& !e.Data.Contains(CustomFormat))
                    e.DragEffects = DragDropEffects.None;


                var point = e.GetPosition(sender as Control);
                clearLastAdornerLayer();
                //var found = listBox.ItemContainerGenerator.Containers.LastOrDefault(info =>
                //{
                //    var x = info.ContainerControl;
                //    return point.Y >= x.Bounds.Top && point.Y <= x.Bounds.Bottom;
                //}, listBox.ItemContainerGenerator.Containers.Last());

                var found = listBox.GetRealizedContainers().LastOrDefault((Func<Control, bool>)(container =>
                {
                    return point.Y >= container.Bounds.Top && point.Y <= container.Bounds.Bottom;
                }), listBox.GetRealizedContainers().Last());


                if (found == null)
                {
                    return;
                }

                lastAdornerElement = found;// as Control; //(Visual)sender;
                var adornerLayer = AdornerLayer.GetAdornerLayer(lastAdornerElement);

                if (adornerLayer != null)
                {
                    var adornedElement = new Border()
                    {
                        CornerRadius = new CornerRadius(3, 0, 0, 3),
                        BorderThickness = new Thickness(2, 2, 0, 2),
                        BorderBrush = new SolidColorBrush(Color.Parse("#9a93cd")),
                        IsHitTestVisible = false
                    };
                    adornerLayer.Children.Add(adornedElement);
                    AdornerLayer.SetAdornedElement(adornedElement, lastAdornerElement);
                }
            }

            void Drop(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                //if (e.Data.Contains(DataFormats.Text))
                //DropState.Text = e.Data.GetText();
                if (e.Data.Contains(DataFormats.FileNames))
                {
                    MessageBus.Current.SendMessage(new AddItemToPlaylistMessage(e.Data.GetFileNames().ToList()));

                    //DropState.Text = string.Join(Environment.NewLine, e.Data.GetFileNames() ?? Array.Empty<string>());
                }
                //else if (e.Data.Contains(CustomFormat))
                //    DropState.Text = "Custom: " + e.Data.Get(DropState);

                clearLastAdornerLayer();
            }

            void DragLeave(object? sender, RoutedEventArgs e)
            {
                clearLastAdornerLayer();
            }


            void clearLastAdornerLayer()
            {
                if (lastAdornerElement != null)
                {
                    var adornerLayer = AdornerLayer.GetAdornerLayer(lastAdornerElement);
                    adornerLayer?.Children.Clear();
                    lastAdornerElement = null;
                }
            }

            //dragMe.PointerPressed += DoDrag;

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        }
    }
}
