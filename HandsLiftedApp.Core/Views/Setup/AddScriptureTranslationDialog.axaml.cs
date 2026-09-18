using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class AddScriptureTranslationDialog : Window
    {
        public (string Label, string Directory, string TranslationCode)? Result { get; private set; }

        private ScriptureTranslationCatalog.Translation[] _translations = Array.Empty<ScriptureTranslationCatalog.Translation>();
        private bool _directoryManuallyEdited;
        private bool _settingDirectoryProgrammatically;

        private CancellationTokenSource? _downloadCts;

        // Set once a download finishes with SOME (but not all) books failed. While this is set,
        // the Download button becomes "Add Anyway" - a second, explicit action required before the
        // partially-downloaded translation is actually added, so the failure message in StatusText
        // is guaranteed to be seen rather than flashing by as the dialog closes underneath it.
        private (string Name, string Directory, string Code)? _pendingPartialResult;

        public AddScriptureTranslationDialog()
        {
            InitializeComponent();
            DownloadButton.IsEnabled = false;
            AddWithoutDownloadingButton.IsEnabled = false;

            _ = LoadTranslationsAsync();
        }

        private async Task LoadTranslationsAsync()
        {
            LoadingText.Text = "Loading translations…";
            LoadingText.IsVisible = true;
            RetryButton.IsVisible = false;
            DownloadButton.IsEnabled = false;
            AddWithoutDownloadingButton.IsEnabled = false;

            try
            {
                var catalog = new ScriptureTranslationCatalog();
                var translations = await catalog.GetPublicDomainEnglishTranslationsAsync();

                _translations = translations.ToArray();
                TranslationComboBox.ItemsSource = _translations.Select(t => t.Name).ToList();
                if (_translations.Length > 0)
                {
                    TranslationComboBox.SelectedIndex = 0;
                    LoadingText.IsVisible = false;
                }
                else
                {
                    LoadingText.Text = "No translations available.";
                    LoadingText.IsVisible = true;
                }

                DownloadButton.IsEnabled = _translations.Length > 0;
                AddWithoutDownloadingButton.IsEnabled = _translations.Length > 0;
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Couldn't load translation list: {ex.Message}";
                LoadingText.IsVisible = true;
                RetryButton.IsVisible = true;
            }
        }

        private void OnRetryTranslationsClick(object? sender, RoutedEventArgs e)
        {
            _ = LoadTranslationsAsync();
        }

        private void OnTranslationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_directoryManuallyEdited) return;
            if (TranslationComboBox.SelectedIndex < 0) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];

            // Setting Text below synchronously fires OnDirectoryTextChanged. That handler must be
            // able to tell this programmatic assignment apart from a genuine user edit - without
            // the flag, it has no way to distinguish the two, and would latch _directoryManuallyEdited
            // permanently true on this very first (self-inflicted) write, before the user ever gets
            // a chance to pick anything. That would silently break auto-fill for every subsequent
            // translation pick, even though the user never touched the folder box.
            _settingDirectoryProgrammatically = true;
            try
            {
                DirectoryTextBox.Text = Path.Combine(Constants.APP_DATA_DIR, "ScriptureData", selected.Abbrev);
            }
            finally
            {
                _settingDirectoryProgrammatically = false;
            }
        }

        private void OnDirectoryTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Only a genuine user edit should stop future auto-fill. Text changes made by
            // OnTranslationSelectionChanged itself are marked via _settingDirectoryProgrammatically
            // and must not count - otherwise the dialog's own bootstrap write (from the initial
            // SelectedIndex = 0 in LoadTranslationsAsync, or any later programmatic re-fill) would
            // permanently disable auto-fill before the user ever typed a character.
            if (_settingDirectoryProgrammatically) return;
            _directoryManuallyEdited = true;
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            var startFolder = !string.IsNullOrWhiteSpace(DirectoryTextBox.Text)
                ? await StorageProvider.TryGetFolderFromPathAsync(DirectoryTextBox.Text)
                : null;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Translation Folder",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                DirectoryTextBox.Text = folders[0].TryGetLocalPath();
            }
        }

        private void OnAddWithoutDownloadingClick(object? sender, RoutedEventArgs e)
        {
            if (TranslationComboBox.SelectedIndex < 0) return;
            if (string.IsNullOrWhiteSpace(DirectoryTextBox.Text)) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];
            Result = (selected.Name, DirectoryTextBox.Text, selected.Code);
            Close();
        }

        private async void OnDownloadClick(object? sender, RoutedEventArgs e)
        {
            // Second click after a partial-failure download: the user has seen the failure message
            // and is explicitly choosing to add the translation anyway, rather than the click
            // re-triggering a fresh download.
            if (_pendingPartialResult is { } pending)
            {
                Result = pending;
                Close();
                return;
            }

            if (TranslationComboBox.SelectedIndex < 0) return;
            if (string.IsNullOrWhiteSpace(DirectoryTextBox.Text)) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];
            var directory = DirectoryTextBox.Text;

            SetDownloadingState(true);
            var totalBooks = ScriptureUsxDownloader.AllBookCodes.Count;
            StatusText.Text = $"Downloading... 0/{totalBooks} books";

            var progress = new Progress<(int done, int total)>(p =>
            {
                StatusText.Text = $"Downloading... {p.done}/{p.total} books";
            });

            _downloadCts = new CancellationTokenSource();

            try
            {
                var downloader = new ScriptureUsxDownloader();
                var failedCount = await Task.Run(
                    () => downloader.DownloadAllBooksAsync(directory, selected.Code, progress, _downloadCts.Token));

                if (failedCount == 0)
                {
                    Result = (selected.Name, directory, selected.Code);
                    Close();
                    return;
                }

                if (failedCount >= totalBooks)
                {
                    // Every single book failed - don't create a Scripture Library row pointing at an
                    // empty/unusable folder. Leave Result unset and let the user retry or cancel.
                    StatusText.Text = "Download failed — no books were retrieved. Check your connection and try again.";
                    SetDownloadingState(false);
                    return;
                }

                // Partial failure: surface the message and require the explicit "Add Anyway" second
                // click (handled at the top of this method) before adding the row, so the message is
                // actually seen rather than the dialog closing in the same synchronous continuation.
                StatusText.Text = $"Downloaded with {failedCount} book(s) failed (see log). You can still use this translation.";
                _pendingPartialResult = (selected.Name, directory, selected.Code);
                DownloadButton.Content = "Add Anyway";
                DownloadButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Download canceled.";
                SetDownloadingState(false);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Download failed: {ex.Message}";
                SetDownloadingState(false);
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private void SetDownloadingState(bool downloading)
        {
            DownloadButton.Content = "Download";
            DownloadButton.IsEnabled = !downloading;
            CancelButton.IsEnabled = true;
            TranslationComboBox.IsEnabled = !downloading;
            AddWithoutDownloadingButton.IsEnabled = !downloading && _translations.Length > 0;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            if (_downloadCts != null)
            {
                // A download is in flight: cancel it instead of closing out from under it. The
                // OperationCanceledException catch in OnDownloadClick resets the UI once the
                // downloader observes the cancellation.
                _downloadCts.Cancel();
                return;
            }

            Close();
        }
    }
}
