using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.Views.Setup;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class WelcomeWindow : Window
    {
        // flag indicating whether the user has selected an action that should result in opening the MainWindow
        private bool _openMainOnClose = false;
        
        public WelcomeWindow()
        {
            InitializeComponent();

            DateTime date = DateTime.Now;
            DayOfWeekString.Text = date.ToString("dddd");
            DateString.Text = date.ToString("d MMMM yyyy");
            GreetingText.Text = date.Hour < 12 ? "Good Morning" :
                               date.Hour < 18 ? "Good Afternoon" : "Good Evening";

            this.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.N && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
                {
                    _openMainOnClose = true;
                    MessageBus.Current.SendMessage(new NewPlaylistAction());
                    Close();
                    e.Handled = true;
                }
                else if (e.Key == Avalonia.Input.Key.O && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
                {
                    _ = OpenFileAsync();
                    e.Handled = true;
                }
            };

            this.Closed += (_, __) =>
            {
                if (_openMainOnClose)
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        // check if MainWindow already exists
                        if (desktopLifetime.Windows.FirstOrDefault(w => w is MainWindow) is MainWindow existingMainWindow)
                        {
                            // bring existing MainWindow to front
                            existingMainWindow.Activate();
                        }
                        else
                        {
                            // create a new MainWindow
                            var main = new MainWindow
                            {
                                DataContext = Globals.Instance.MainViewModel,
                                WindowState = WindowState.Normal
                            };

                            desktopLifetime.MainWindow = main;
                            main.Show();
                        }
                    }
                }
                else
                {
                    // no action was selected
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        // terminate the application if the MainWindow is not already open
                        var mainIsOpen = desktopLifetime.MainWindow is MainWindow { IsVisible: true };
                        if (!mainIsOpen)
                        {
                            App.ExitApplication(desktopLifetime, this);
                        }
                    }
                }
            };
        }

        private void LoadRecentPlaylistEntryCommand(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: WelcomeWindowViewModel.RecentPlaylistEntry recentPlaylistEntry })
            {
                _openMainOnClose = true;
                MessageBus.Current.SendMessage(new LoadPlaylistAction() {FilePath = recentPlaylistEntry.FilePath});
                Close();
            }
        }

        private void NewClicked(object? sender, RoutedEventArgs e)
        {
            _openMainOnClose = true;
            MessageBus.Current.SendMessage(new NewPlaylistAction());
            Close();
        }

        private void SetUpClicked(object? sender, RoutedEventArgs e)
        {
            SetupWindow setupWindow = new SetupWindow();
            setupWindow.ShowDialog(this);
        }
        
        private void LibraryClicked(object? sender, RoutedEventArgs e)
        {
            LibraryWindow w = new LibraryWindow() { DataContext = Globals.Instance.MainViewModel };
            w.ShowDialog(this);
        }
        
        private void ExitClicked(object? sender, RoutedEventArgs e)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                App.ExitApplication(desktopLifetime, this);
            }
        }
        
        private async Task OpenFileAsync()
        {
            var topLevel = GetTopLevel(this);
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Text File",
                AllowMultiple = false
            });
            if (files.Count >= 1)
            {
                var filePath = files[0].Path.LocalPath;
                _openMainOnClose = true;
                MessageBus.Current.SendMessage(new LoadPlaylistAction() { FilePath = filePath });
                Close();
            }
        }

        private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args) => await OpenFileAsync();

        private void OpenFileLocation_Clicked(object? sender, RoutedEventArgs e)
        {
            var entry = GetEntryFromContextMenuSender(sender);
            if (entry is null) return;
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{entry.FilePath}\"");
        }

        private void RemoveFromRecents_Clicked(object? sender, RoutedEventArgs e)
        {
            var entry = GetEntryFromContextMenuSender(sender);
            if (entry is null) return;
            var vm = DataContext as WelcomeWindowViewModel;
            vm?.RecentPlaylists.Remove(entry);
            if (vm?._parent?.settings is { } s && s.RecentPlaylistFullPathsList is { } list)
            {
                s.RecentPlaylistFullPathsList = System.Array.FindAll(list, p => p != entry.FilePath);
            }
        }

        private static WelcomeWindowViewModel.RecentPlaylistEntry? GetEntryFromContextMenuSender(object? sender)
        {
            // ContextMenu items don't inherit DataContext through popup boundary.
            // We use Tag="{Binding}" on the Button as the workaround.
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is Button btn)
            {
                return btn.Tag as WelcomeWindowViewModel.RecentPlaylistEntry;
            }
            return null;
        }
    }
}