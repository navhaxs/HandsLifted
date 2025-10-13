using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.AddItem;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.AddItem.Pages
{
    public partial class StartView : UserControl
    {
        public StartView()
        {
            InitializeComponent();

            // GotFocus += delegate { SearchBox.Focus(); };
        }

        private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is StartViewModel vm)
            {
                RunImport(vm.AddItemViewModel);
            }
        }

        private void CreateSlideButton_OnClick(object? sender, RoutedEventArgs e)
        {
            SlideEditorWindow w = new();
            w.Show();
        }

        private async void RunImport(AddItemViewModel vm)
        {
            var filePaths = await vm.ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
            if (filePaths != null && filePaths.Length > 0)
            {
                MessageBus.Current.SendMessage(new AddItemByFilePathMessage(new List<string>(filePaths), vm.ItemInsertIndex));
                CloseWindow();
            }
        }

        private void MusicButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                if (DataContext is AddItemPageViewModel vm)
                {
                    vm.AddItemViewModel.Page =
                        new ResultsViewModel(vm.AddItemViewModel, control.DataContext as Models.Library.Library);
                }
            }
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

                CloseWindow();
            }
        }

        private void SearchBox_OnSearchButtonClick(object? sender, RoutedEventArgs e)
        {
            // Handle search button click - implement your search logic here
            if (sender is Controls.SearchBox searchBox)
            {
                var searchText = searchBox.SearchText;
                // TODO: Implement search functionality
            }
        }

        private void CloseWindow()
        {
            var t = TopLevel.GetTopLevel(this);
            if (t is Window parentWindow)
            {
                parentWindow.Close();
            }
        }

        private void SearchBox_OnSearchClicked(object? sender, RoutedEventArgs e)
        {
            // throw new NotImplementedException();
        }
    }
}