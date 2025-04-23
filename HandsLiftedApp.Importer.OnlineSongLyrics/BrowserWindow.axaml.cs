using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Importer.OnlineSongLyrics.Clipboard;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public partial class BrowserWindow : Window
    {
        private WindowsHwndSource windowClipboardManager;

        public BrowserWindow()
        {
            if (!Design.IsDesignMode)
            {
                var path = Path.Join(Directory.GetCurrentDirectory(), "webviewCache");
                Directory.CreateDirectory(path);
            }

   
            // WebView.Settings.OsrEnabled = false;
            //WebView.Settings.CachePath = path;
            // WebView.Settings.LogFile = "ceflog.txt";
            // WebView.Settings.EnableErrorLogOnly = true;
            InitializeComponent();
            // DataContext = new BrowserViewModel(PART_WebView);
            
            // PART_WebView.ShowDeveloperTools();

            if (Design.IsDesignMode)
            {
                return;
            }

            Opened += ((sender, args) =>
            {
                IntPtr handle = TryGetPlatformHandle().Handle;
                Debug.Print(handle.ToString());

                // Initialize the clipboard now that we have a window soruce to use
                windowClipboardManager = WindowsHwndSource.FromHwnd(handle);
                windowClipboardManager.ClipboardChanged += ClipboardChanged;
                
                PART_WebView.Url = new Uri("https://songselect.com");
            });

            Closing += ((sender, args) =>
            {
            });
            // PART_WebView.BeforeNavigate += (request) =>
            // {
            //     Dispatcher.UIThread.InvokeAsync(() => PART_ProgressBar.IsVisible = true);
            // };
            //
            // PART_WebView.Navigated += (url, frameName) => {
            //     Dispatcher.UIThread.InvokeAsync(() => PART_ProgressBar.IsVisible = false);
            // };
        }

        private void ClipboardChanged(object sender, EventArgs e)
        {
            // Create a new task that represents the asynchronous operation
            Clipboard.GetTextAsync()
                .ContinueWith(t =>
                {
                    string result = t.Result;

                    // todo: validation
                    // string must have:
                    // - song title (... or not)
                    // - stanza > at least 1

                    Dispatcher.UIThread.InvokeAsync(() => { PART_SelectionText.Text = result; });
                    Console.WriteLine($"The final result is: {result}");
                });
        }

        private void BackButton_OnClick(object? sender, RoutedEventArgs e)
        {
            PART_WebView.GoBack();
        }

        private void ForwardButton_OnClick(object? sender, RoutedEventArgs e)
        {
            PART_WebView.GoForward();
        }

        private void ReloadButton_OnClick(object? sender, RoutedEventArgs e)
        {
            PART_WebView.Reload();
        }
    }
}