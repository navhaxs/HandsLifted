using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Slides.v1;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Importer.Scripture;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class SetupWindow : Window
    {
        private static readonly string[] GoogleScopes =
            { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveReadonly };

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

            if (!Design.IsDesignMode)
            {
                RefreshGoogleSignInStatus();
            }

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

        private void DownloadScriptureDataButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var button = this.Get<Button>("DownloadScriptureDataButton");
            var statusText = this.Get<TextBlock>("ScriptureDownloadStatusText");
            var rootPath = Globals.Instance.AppPreferences.ScriptureDataPath;
            var totalBooks = ScriptureUsxDownloader.AllBookCodes.Count;

            button.IsEnabled = false;
            statusText.IsVisible = true;
            statusText.Text = $"Downloading... 0/{totalBooks} books";

            var progress = new Progress<(int done, int total)>(p =>
            {
                statusText.Text = $"Downloading... {p.done}/{p.total} books";
            });

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var downloader = new ScriptureUsxDownloader();
                    var failedCount = await downloader.DownloadAllBooksAsync(rootPath, progress);

                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = failedCount == 0
                            ? "Download complete."
                            : $"Downloaded {totalBooks - failedCount} of {totalBooks} books; {failedCount} failed (see log).";
                        button.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = $"Download failed: {ex.Message}";
                        button.IsEnabled = true;
                    });
                }
            });
        }

        private void SignInWithGoogle_OnClick(object? sender, RoutedEventArgs e)
        {
            var clientId = Globals.Instance.AppPreferences.GoogleClientId;
            var clientSecret = Globals.Instance.AppPreferences.GoogleClientSecret;

            var statusText = this.Get<TextBlock>("SignInStatusText");
            var button = this.Get<Button>("SignInWithGoogleButton");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                SetGoogleSignInStatus("Enter Client ID and Client Secret first.", GoogleSignInStatus.Error);
                return;
            }

            button.IsEnabled = false;
            SetGoogleSignInStatus("Opening browser...", GoogleSignInStatus.Neutral);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    GoogleWebAuthorizationBroker.AuthorizeAsync(
                        new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                        GoogleScopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore("token.json", true)).Wait();

                    Dispatcher.UIThread.Post(() =>
                    {
                        button.IsEnabled = true;
                        RefreshGoogleSignInStatus();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SetGoogleSignInStatus($"Sign-in failed: {ex.Message}", GoogleSignInStatus.Error);
                        button.IsEnabled = true;
                    });
                }
            });
        }

        private enum GoogleSignInStatus { Ok, Error, Neutral }

        private void SetGoogleSignInStatus(string text, GoogleSignInStatus status)
        {
            var statusText = this.Get<TextBlock>("SignInStatusText");
            statusText.Text = text;
            statusText.IsVisible = true;
            statusText.Foreground = status switch
            {
                GoogleSignInStatus.Ok => new SolidColorBrush(Colors.MediumSeaGreen),
                GoogleSignInStatus.Error => new SolidColorBrush(Colors.OrangeRed),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        // Checks the stored OAuth token non-interactively (no browser popup) so the panel can show
        // whether the user is currently signed in, expired (silently refreshed if possible), or not
        // signed in at all.
        private void RefreshGoogleSignInStatus()
        {
            var clientId = Globals.Instance.AppPreferences.GoogleClientId;
            var clientSecret = Globals.Instance.AppPreferences.GoogleClientSecret;

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                SetGoogleSignInStatus("Not signed in.", GoogleSignInStatus.Neutral);
                return;
            }

            SetGoogleSignInStatus("Checking sign-in status...", GoogleSignInStatus.Neutral);

            System.Threading.Tasks.Task.Run(() =>
            {
                string text;
                GoogleSignInStatus status;
                try
                {
                    var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                        Scopes = GoogleScopes,
                        DataStore = new FileDataStore("token.json", true)
                    });

                    var storedToken = flow.LoadTokenAsync("user", CancellationToken.None).GetAwaiter().GetResult();
                    if (storedToken == null)
                    {
                        text = "Not signed in.";
                        status = GoogleSignInStatus.Neutral;
                    }
                    else
                    {
                        var credential = new UserCredential(flow, "user", storedToken);
                        if (credential.Token.IsExpired(SystemClock.Default))
                        {
                            bool refreshed;
                            try { refreshed = credential.RefreshTokenAsync(CancellationToken.None).GetAwaiter().GetResult(); }
                            catch (TokenResponseException) { refreshed = false; }

                            text = refreshed ? "Signed in." : "Sign-in expired — please sign in again.";
                            status = refreshed ? GoogleSignInStatus.Ok : GoogleSignInStatus.Error;
                        }
                        else
                        {
                            text = "Signed in.";
                            status = GoogleSignInStatus.Ok;
                        }
                    }
                }
                catch (Exception ex)
                {
                    text = $"Couldn't check sign-in status: {ex.Message}";
                    status = GoogleSignInStatus.Error;
                }

                Dispatcher.UIThread.Post(() => SetGoogleSignInStatus(text, status));
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