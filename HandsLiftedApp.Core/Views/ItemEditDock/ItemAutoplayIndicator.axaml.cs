using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views.ItemEditDock
{
    public partial class ItemAutoplayIndicator : UserControl
    {
        public ItemAutoplayIndicator()
        {
            InitializeComponent();
        }

        private void ResumeAutoplayButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Globals.Instance.MainViewModel.Playlist.AutoAdvanceTimer.Timer?.Resume();
        }

        private void PauseAutoplayButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Globals.Instance.MainViewModel.Playlist.AutoAdvanceTimer.Timer?.Stop();
        }
    }
}
