using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels;
using WebViewControl;

namespace HandsLiftedApp.Views
{
    public partial class WebBrowserWindow : Window
    {
        public WebBrowserWindow()
        {
            WebView.Settings.OsrEnabled = false;
            WebView.Settings.LogFile = "ceflog.txt";

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            DataContext = new WebBrowserViewModel(this.FindControl<WebView>("webview"));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        }
}
