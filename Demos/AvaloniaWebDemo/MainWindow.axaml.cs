using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WebViewCore.Events;

namespace AvaloniaWebDemo;

public partial class MainWindow : Window
{
    private WindowsHwndSource windowClipboardManager; 
    public MainWindow()
    {
        InitializeComponent();

        // PART_WebView.GoBack();

        Opened += ((sender, args) =>
        {
            IntPtr handle = TryGetPlatformHandle().Handle;
            Debug.Print(handle.ToString());

            // Initialize the clipboard now that we have a window soruce to use
            windowClipboardManager = WindowsHwndSource.FromHwnd(handle);
            windowClipboardManager.ClipboardChanged += ClipboardChanged;
        });
        
        PART_WebView.NavigationStarting += (sender, args) =>
        {
            PART_ProgressBar.IsVisible = true;
        };
        
        PART_WebView.NavigationCompleted += (sender, args) =>
        {
            PART_ProgressBar.IsVisible = false;
        };
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

    private void PART_WebView_OnWebMessageReceived(object? sender, WebViewMessageReceivedEventArgs e)
    {
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