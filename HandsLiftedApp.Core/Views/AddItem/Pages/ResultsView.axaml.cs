using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.AddItem.Pages
{
    public partial class ResultsView : UserControl
    {
        public ResultsView()
        {
            InitializeComponent();
        }

        private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var ctl = sender as Control;
            if (ctl != null)
            {
                FlyoutBase.ShowAttachedFlyout(ctl);
            }
        }

        private void BackButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AddItemPageViewModel vm)
            {
                vm.AddItemViewModel.Page = new StartViewModel(vm.AddItemViewModel);
            }
        }

        private void AddSelectedItemButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ResultsViewModel vm)
            {
                List<string> items = new List<string>() { vm.SelectedLibraryItem.FullFilePath };
                MessageBus.Current.SendMessage(new AddItemByFilePathMessage(items, vm.AddItemViewModel.ItemInsertIndex));
            }
            var t = TopLevel.GetTopLevel(this);
            if (t is Window parentWindow)
            {
                parentWindow.Close();
            }
        }
    }
}