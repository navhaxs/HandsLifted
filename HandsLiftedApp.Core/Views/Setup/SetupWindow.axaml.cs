using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
using HandsLiftedApp.Core.Models.Library.Config;
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

        private void AddMediaLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
        {
            _setupWindowViewModel.AddMediaLibraryRow();
        }

        private void AddSongLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
        {
            _setupWindowViewModel.AddSongLibraryRow();
        }

        private async void AddScriptureLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new AddScriptureTranslationDialog();
            await dialog.ShowDialog(this);

            if (dialog.Result is { } result)
            {
                _setupWindowViewModel.AddScriptureLibraryRow(result.Label, result.Directory, result.TranslationCode);
            }
        }

        private void RemoveLibraryRowButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: LibraryConfig.LibraryDefinition row })
            {
                _setupWindowViewModel.RemoveLibraryRow(row);
            }
        }

        private async void BrowseLibraryDirectoryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control { DataContext: LibraryConfig.LibraryDefinition row }) return;

            var startFolder = !string.IsNullOrWhiteSpace(row.Directory)
                ? await StorageProvider.TryGetFolderFromPathAsync(row.Directory)
                : null;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Library Folder",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                row.Directory = folders[0].TryGetLocalPath();
            }
        }

        private async void BrowseMediaLibraryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            var currentPath = Globals.Instance.AppPreferences.MediaLibraryPath;
            var startFolder = !string.IsNullOrWhiteSpace(currentPath)
                ? await StorageProvider.TryGetFolderFromPathAsync(currentPath)
                : null;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Media Library Folder",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                Globals.Instance.AppPreferences.MediaLibraryPath = folders[0].TryGetLocalPath();
                Globals.Instance.MainViewModel.LibraryViewModel.ReloadLibraries();
            }
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