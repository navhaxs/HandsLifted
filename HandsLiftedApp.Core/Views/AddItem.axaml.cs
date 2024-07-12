using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class AddItem : UserControl
    {
        public AddItem()
        {
            InitializeComponent();

            GotFocus += delegate { SearchBox.Focus(); };
        }

        private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AddItemViewModel vm)
            {
                RunImport(vm);
            }
        }

        private void CreateSlideButton_OnClick(object? sender, RoutedEventArgs e)
        {
            
        }

        private async void RunImport(AddItemViewModel vm)
        {
            var filePaths = await vm.ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
            if (filePaths != null)
            {
                MessageBus.Current.SendMessage(new AddItemByFilePathMessage(new List<string>(filePaths)));
            }
        }

        private void MusicButton_OnClick(object? sender, RoutedEventArgs e)
        {
            
        }

        private void CreateItemButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Item? nearestItem = null;
                // int? itemInsertIndex = null;
                //
                // var parentAddItemButton = ControlExtension.FindAncestor<AddItemButton>(menuItem);
                //
                var itemInsertIndex = Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex;
                AddItemMessage.AddItemType type;
                Enum.TryParse(button.CommandParameter.ToString(), out type);
                MessageBus.Current.SendMessage(new AddItemMessage { Type = type, InsertIndex = itemInsertIndex });

                Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex = null;

                switch (button.CommandParameter)
                {
                    case "Logo":
                        break;
                    default:
                        break;
                }
                var t = TopLevel.GetTopLevel(this);
                if (t is Window parentWindow)
                {
                    parentWindow.Close();
                }
            }
        }
    }
}