using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Views.App
{
    public partial class MessageWindow : Window
    {
        public MessageWindow(string Title = "", string Message = "")
        {
            InitializeComponent();
        }

        private void OnDismiss(object? sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
