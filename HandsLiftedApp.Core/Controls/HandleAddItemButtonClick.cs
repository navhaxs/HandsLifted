using Avalonia.Controls;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Core.Controls
{
    public static class HandleAddItemButtonClick
    {
        public static void ShowAddWindow(int? itemInsertIndex, object? sender)
        {
            Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex = itemInsertIndex;

            Window? window = null;
            if (sender is Control control)
            {
                window = control.GetVisualRoot() as Window;
            }

            AddItemWindow aiw = new AddItemWindow() { DataContext = Globals.Instance.MainViewModel.AddItemViewModel };
            Globals.Instance.MainViewModel.AddItemViewModel.Page = new StartViewModel(Globals.Instance.MainViewModel.AddItemViewModel);
            aiw.ViewModel.ItemInsertIndex = itemInsertIndex;
            if (window == null)
            {
                aiw.Show();
            }
            else
            {
                aiw.ShowDialog(window);
            }
        } 
    }
}