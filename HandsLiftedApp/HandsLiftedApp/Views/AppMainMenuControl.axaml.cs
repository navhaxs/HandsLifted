using Avalonia.Controls;
using HandsLiftedApp.Models.UI;
using ReactiveUI;

namespace HandsLiftedApp.Views
{
    public partial class AppMainMenuControl : UserControl
    {
        public AppMainMenuControl()
        {
            InitializeComponent();
        }

        private void CloseWindow(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new MainWindowMessage());
        }
    }
}
