using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            sAsync();
        }

        private async Task sAsync()
        {
            await Task.Run(() => {
                Task.Delay(3 * 1000).Wait();
                Dispatcher.UIThread.InvokeAsync(() => Close());
            });
        }
    }
}
