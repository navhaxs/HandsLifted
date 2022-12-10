using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Views.App
{
    public partial class ExitConfirmationWindow : Window
    {
        public ExitConfirmationWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.Closed += ExitConfirmationWindow_Closed;
         
        }

        private void ExitConfirmationWindow_Closed(object? sender, EventArgs e)
        {
                if (callback != null)
                    callback();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnExit(object? sender, RoutedEventArgs e)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Shutdown();
            }
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        public Action callback = null;

    }
}
