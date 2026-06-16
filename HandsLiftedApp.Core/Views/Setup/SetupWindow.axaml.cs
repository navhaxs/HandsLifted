using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Slides.v1;
using Google.Apis.Util.Store;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class SetupWindow : Window
    {
        SetupWindowViewModel _setupWindowViewModel;
 
        public SetupWindow()
        {
            InitializeComponent();
            this.DataContext = _setupWindowViewModel = new SetupWindowViewModel(this.Screens);
            this.Closed += PreferencesWindow_Closed;
            
            this.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
            
            Win10DropshadowWorkaround.Register(this);
            
            var themeVariants = this.Get<ComboBox>("ThemeVariants");
            themeVariants.SelectedItem = Application.Current!.RequestedThemeVariant;
            themeVariants.SelectionChanged += (sender, e) =>
            {
                if (themeVariants.SelectedItem is ThemeVariant themeVariant)
                {
                    Application.Current!.RequestedThemeVariant = themeVariant;
                }
            };
        }

        private void PreferencesWindow_Closed(object? sender, EventArgs e)
        {_setupWindowViewModel.HideDisplayItentification();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (IdentifyToggleButton.IsChecked == true)
            {
                _setupWindowViewModel.ShowDisplayIdentification(Screens);
            }
            else
            {
                _setupWindowViewModel.HideDisplayItentification();
            }
        }

        private void EditLibraryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start("notepad.exe", Constants.LIBRARY_CONFIG_FILEPATH);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", Constants.LIBRARY_CONFIG_FILEPATH);
            }
            else
            {
                // For Linux and other platforms, try using the default text editor
                Process.Start("xdg-open", Constants.LIBRARY_CONFIG_FILEPATH);
            }
        }

        private void ReloadLibraryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Globals.Instance.MainViewModel.LibraryViewModel.ReloadLibraries();
        }

        private void SignInWithGoogle_OnClick(object? sender, RoutedEventArgs e)
        {
            var clientId = Globals.Instance.AppPreferences.GoogleClientId;
            var clientSecret = Globals.Instance.AppPreferences.GoogleClientSecret;

            var statusText = this.Get<TextBlock>("SignInStatusText");
            var button = this.Get<Button>("SignInWithGoogleButton");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                statusText.Text = "Enter Client ID and Client Secret first.";
                statusText.IsVisible = true;
                return;
            }

            button.IsEnabled = false;
            statusText.Text = "Opening browser...";
            statusText.IsVisible = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string[] scopes = { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveReadonly };
                    GoogleWebAuthorizationBroker.AuthorizeAsync(
                        new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                        scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore("token.json", true)).Wait();

                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = "Signed in successfully.";
                        button.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = $"Sign-in failed: {ex.Message}";
                        button.IsEnabled = true;
                    });
                }
            });
        }

        private void DoneButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ProjectorOutput_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0)
                MessageBus.Current.SendMessage(new OutputDisplayConfigurationChangeMessage() { ChangedDisplay = OutputDisplayConfigurationChangeMessage.Display.Projector });
        }
        
        private void StageOutput_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0)
                MessageBus.Current.SendMessage(new OutputDisplayConfigurationChangeMessage() { ChangedDisplay = OutputDisplayConfigurationChangeMessage.Display.StageDisplay });
        }
    }
}