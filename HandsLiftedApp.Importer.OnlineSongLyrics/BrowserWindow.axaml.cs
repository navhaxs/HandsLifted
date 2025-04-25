using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Importer.OnlineSongLyrics.Clipboard;
using ReactiveUI;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public partial class BrowserWindow : Window
    {
        public static string TEST_DATA = @"Before The Throne Of God

Verse 1
Before the throne of God above
I have a strong and perfect plea
A great High Priest whose name is Love
Who ever lives and pleads for me

Verse 2
My name is graven on His hands
My name is written on His heart
I know that while in heaven He stands
No tongue can bid me thence depart

Verse 3
When Satan tempts me to despair
And tells me of the guilt within
Upward I look and see Him there
Who made an end of all my sin

Verse 4
Because the sinless Saviour died
My sinful soul is counted free
For God the Just is satisfied
To look on Him and pardon me

Verse 5
Behold Him there the risen Lamb
My perfect spotless Righteousness
The great unchangeable I AM
The King of glory and of grace

Verse 6
One with Himself I cannot die
My soul is purchased by His blood
My life is hid with Christ on high
With Christ my Saviour and my God

Charitie Lees Bancroft
CCLI Song #990391
© Public Domain
For use solely with the SongSelect® Terms of Use.  All rights reserved. www.ccli.com
CCLI License #317371";

        public event EventHandler<string>? TextSelected;

        private WindowsHwndSource windowClipboardManager;

        public BrowserWindow()
        {
            if (!Design.IsDesignMode)
            {
                var path = Path.Join(Directory.GetCurrentDirectory(), "webviewCache");
                Directory.CreateDirectory(path);
            }

            DataContext = new BrowserWindowViewModel();

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

            Closing += ((sender, args) => { });
            // PART_WebView.BeforeNavigate += (request) =>
            // {
            //     Dispatcher.UIThread.InvokeAsync(() => PART_ProgressBar.IsVisible = true);
            // };
            //
            // PART_WebView.Navigated += (url, frameName) => {
            //     Dispatcher.UIThread.InvokeAsync(() => PART_ProgressBar.IsVisible = false);
            // };
        }

        protected virtual void OnTextSelected(string selectedText)
        {
            TextSelected?.Invoke(this, selectedText);
        }

        private void ClipboardChanged(object sender, EventArgs e)
        {
            // Create a new task that represents the asynchronous operation
            Clipboard.GetTextAsync()
                .ContinueWith(t =>
                {
                    string result = t.Result;


                    string TEST_DATA = @"Before The Throne Of God

Verse 1
Before the throne of God above
I have a strong and perfect plea
A great High Priest whose name is Love
Who ever lives and pleads for me";

                    // todo: validation
                    // string must have:
                    // - song title (... or not)
                    // - stanza > at least 1

                    // if (result.Split('\n').Length > 1)

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (DataContext is BrowserWindowViewModel vm)
                        {
                            vm.SelectedClipboardData = result;
                        }
                    });
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

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            Carousel.SelectedIndex = 1;
        }

        // Debug methods
        private void WarningButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Clipboard.SetTextAsync(TEST_DATA);
        }

        private void WarningButton2_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is BrowserWindowViewModel vm)
            {
                vm.SelectedClipboardData = string.Empty;
            }

            Carousel.SelectedIndex = 0;
        }

        private void ImportSongButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is BrowserWindowViewModel vm)
            {
                if (!string.IsNullOrEmpty(vm.SelectedClipboardData))
                {
                    OnTextSelected(vm.SelectedClipboardData);
                }
            }

            Close();
        }
    }
}