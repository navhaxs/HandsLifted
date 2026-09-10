using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels.AddItem;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Importer.GoogleSlides;
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
                        new ResultsViewModel(vm.AddItemViewModel, control.DataContext as Library);
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
            if (sender is SearchBox searchBox)
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

        private async void ButtonImportGoogleSlides_OnClick(object? sender, RoutedEventArgs e)
        {
            Window? window = FindOwnerWindow();
            if (window == null) return;

            var GoogleSlidesPresentationId = await ImportWizard.Run(window);

            if (GoogleSlidesPresentationId == null)
            {
                // abort
                return;
            }

            var itemInsertIndex = Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex;
            MessageBus.Current.SendMessage(new AddItemMessage() {InsertIndex = itemInsertIndex, Type = AddItemMessage.AddItemType.GoogleSlides, CreateInfo = GoogleSlidesPresentationId});
            CloseWindow();
        }

        private async void ButtonImportOnlineVideo_OnClick(object? sender, RoutedEventArgs e)
        {
            Window? window = FindOwnerWindow();
            if (window == null) return;

            var dialog = new OnlineVideoUrlDialog();
            await dialog.ShowDialog(window);

            if (dialog.Result == null)
            {
                // abort
                return;
            }

            var itemInsertIndex = Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex;
            MessageBus.Current.SendMessage(new AddItemMessage() {InsertIndex = itemInsertIndex, Type = AddItemMessage.AddItemType.OnlineVideo, CreateInfo = dialog.Result});
            CloseWindow();
        }

        // this.VisualRoot / TopLevel.GetTopLevel(this) have been observed returning null here even
        // though StartView is hosted directly inside AddItemWindow (see logs/visionscreens_app_log.txt
        // 00:15:43 fatal ArgumentNullException from ButtonImportGoogleSlides_OnClick). Walk the logical
        // tree instead, matching AddItemFlyoutResourceDictionary.OnMenuItemClick's Scripture case.
        private Window? FindOwnerWindow()
        {
            Control? ancestor = this;
            while (ancestor != null)
            {
                if (ancestor is Window w) return w;
                ancestor = ancestor.Parent as Control;
            }

            return null;
        }

        private async void ButtonCreateFreeTextSlide_OnClick(object? sender, RoutedEventArgs e)
        {
            var itemInsertIndex = Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex;
            MessageBus.Current.SendMessage(new AddItemMessage() {InsertIndex = itemInsertIndex, Type = AddItemMessage.AddItemType.BlankGroup});
            CloseWindow();
        }
    }
}