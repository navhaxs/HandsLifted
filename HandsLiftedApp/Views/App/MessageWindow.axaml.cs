using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Logic;
using System.Threading;

namespace HandsLiftedApp.Views.App
{
    public partial class MessageWindow : Window
    {
        public MainWindow parentWindow;
        public MessageWindow(string Title = "", string Message = "")
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void OnDismiss(object? sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
