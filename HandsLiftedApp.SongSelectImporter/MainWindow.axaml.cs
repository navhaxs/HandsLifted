using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace HandsLiftedApp.SongSelectImporter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        WebView.Settings.OsrEnabled = false;
        WebView.Settings.LogFile = "ceflog.txt";
        AvaloniaXamlLoader.Load(this);

        DataContext = new MainWindowViewModel(this.FindControl<WebView>("webview"));    }
}