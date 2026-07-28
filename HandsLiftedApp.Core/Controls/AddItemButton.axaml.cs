using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Controls
{
    public partial class AddItemButton : UserControl
    {
        private static readonly DataFormat<object> CustomFormat =
            DataFormat.CreateInProcessFormat<object>("application/xxx-avalonia-controlcatalog-custom");

        public static readonly StyledProperty<int?> ItemInsertIndexProperty =
            AvaloniaProperty.Register<AddItemButton, int?>(nameof(ItemInsertIndex));

        public int? ItemInsertIndex
        {
            get { return GetValue(ItemInsertIndexProperty); }
            set { SetValue(ItemInsertIndexProperty, value); }
        }

        private readonly TextBlock _dropState;

        public AddItemButton()
        {
            InitializeComponent();
            _dropState = this.Get<TextBlock>("DropState");

            AddButton.PointerEntered += (object? sender, PointerEventArgs e) => { AddButtonTooltip.IsVisible = true; };
            AddButton.PointerExited += (object? sender, PointerEventArgs e) => { AddButtonTooltip.IsVisible = false; };

            Globals.Instance?.MainViewModel?.Playlist?.WhenAnyValue(x => x.ActiveItemInsertIndex)
                .Subscribe((int? x) =>
                {
                    if (ItemInsertIndex == x && x != null)
                    {
                        AddButton.Classes.Add("active");
                    }
                    else
                    {
                        AddButton.Classes.Remove("active");
                    }
                });

            SetupDnd("Files",
                async d =>
                {
                    var file = await (VisualRoot as TopLevel)!.StorageProvider.TryGetFileFromPathAsync(
                        Assembly.GetEntryAssembly()?.GetModules().FirstOrDefault()?.FullyQualifiedName);
                    if (file != null)
                        d.Add(DataTransferItem.Create(DataFormat.File, file));
                }, DragDropEffects.Copy);
        }

        private void AddContentButton_OnClick(object? sender, RoutedEventArgs e)
        {
            // int itemInsertIndex;
            //
            // if (ItemInsertIndex != null)
            // {
            //     itemInsertIndex = ItemInsertIndex.Value;
            // }
            // else if (DataContext is Item item)
            // {
            //     itemInsertIndex = Globals.Instance.MainViewModel.Playlist.Items.IndexOf(item) + 1;
            // }
            // else
            // {
            //     itemInsertIndex = Globals.Instance.MainViewModel.Playlist.Items.Count;
            // }
            //
            // HandleAddItemButtonClick.ShowAddWindow(itemInsertIndex, sender);
        }

        void SetupDnd(string suffix, Func<DataTransfer, Task> factory, DragDropEffects effects)
        {
            //var dragMe = this.Get<Border>("DragMe" + suffix);
            //var dragState = this.Get<TextBlock>("DragState" + suffix);

            async void DoDrag(object? sender, Avalonia.Input.PointerPressedEventArgs e)
            {
                //var dragData = new DataObject();
                //factory(dragData);

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

                this.Background = SolidColorBrush.Parse("Red");

                // Only allow if the dragged data contains text or filenames.
                if (!e.DataTransfer.Contains(DataFormat.Text)
                    && !e.DataTransfer.Contains(DataFormat.File)
                    && !e.DataTransfer.Contains(CustomFormat))
                    e.DragEffects = DragDropEffects.None;
            }

            void DragLeave(object? sender, DragEventArgs e)
            {
                this.Background = SolidColorBrush.Parse("Transparent");
            }

            async void Drop(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                if (e.DataTransfer.Contains(DataFormat.Text))
                {
                    _dropState.Text = e.DataTransfer.TryGetText();
                }
                else if (e.DataTransfer.Contains(DataFormat.File))
                {
                    var files = e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>();
                    var contentStr = "";

                    var listOfFilePaths = new List<string>();
                    foreach (var item in files)
                    {
                        if (item is IStorageFile file)
                        {
                            listOfFilePaths.Add(file.Path.LocalPath);

                            //var content = await DialogsPage.ReadTextFromFile(file, 500);
                            var content = "content";
                            contentStr +=
                                $"File {item.Name}:{Environment.NewLine}{content}{Environment.NewLine}{Environment.NewLine} inserted at {ItemInsertIndex}";
                        }
                        else if (item is IStorageFolder folder)
                        {
                            // TODO ....
                            var childrenCount = 0;
                            await foreach (var _ in folder.GetItemsAsync())
                            {
                                childrenCount++;
                            }

                            contentStr +=
                                $"Folder {item.Name}: items {childrenCount}{Environment.NewLine}{Environment.NewLine}";
                        }
                    }

                    MessageBus.Current.SendMessage(new AddItemByFilePathMessage(listOfFilePaths, ItemInsertIndex));

                    _dropState.Text = contentStr;
                }
                //else if (e.DataTransfer.Contains(CustomFormat))
                //{
                //    _dropState.Text = "Custom: " + e.DataTransfer.TryGetValue(CustomFormat);
                //}

                this.Background = SolidColorBrush.Parse("Transparent");
            }

            //dragMe.PointerPressed += DoDrag;

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        }
    }
}