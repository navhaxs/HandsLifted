using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class AddScriptureTranslationDialog : Window
    {
        public (string Label, string Directory, string TranslationCode)? Result { get; private set; }

        private ScriptureTranslationCatalog.Translation[] _translations = Array.Empty<ScriptureTranslationCatalog.Translation>();
        private bool _directoryManuallyEdited;
        private bool _settingDirectoryProgrammatically;

        public AddScriptureTranslationDialog()
        {
            InitializeComponent();
            DownloadButton.IsEnabled = false;

            _ = LoadTranslationsAsync();
        }

        private async Task LoadTranslationsAsync()
        {
            try
            {
                var catalog = new ScriptureTranslationCatalog();
                var translations = await catalog.GetPublicDomainEnglishTranslationsAsync();

                _translations = translations.ToArray();
                TranslationComboBox.ItemsSource = _translations.Select(t => t.Name).ToList();
                if (_translations.Length > 0)
                {
                    TranslationComboBox.SelectedIndex = 0;
                }

                LoadingText.IsVisible = false;
                DownloadButton.IsEnabled = _translations.Length > 0;
            }
            catch (Exception ex)
            {
                LoadingText.Text = $"Couldn't load translation list: {ex.Message}";
            }
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

        private async void OnDownloadClick(object? sender, RoutedEventArgs e)
        {
            if (TranslationComboBox.SelectedIndex < 0) return;
            if (string.IsNullOrWhiteSpace(DirectoryTextBox.Text)) return;

            var selected = _translations[TranslationComboBox.SelectedIndex];
            var directory = DirectoryTextBox.Text;

            DownloadButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            TranslationComboBox.IsEnabled = false;
            var totalBooks = ScriptureUsxDownloader.AllBookCodes.Count;
            StatusText.Text = $"Downloading... 0/{totalBooks} books";

            var progress = new Progress<(int done, int total)>(p =>
            {
                StatusText.Text = $"Downloading... {p.done}/{p.total} books";
            });

            try
            {
                var downloader = new ScriptureUsxDownloader();
                var failedCount = await Task.Run(() => downloader.DownloadAllBooksAsync(directory, selected.Code, progress));

                if (failedCount > 0)
                {
                    StatusText.Text = $"Downloaded with {failedCount} book(s) failed (see log). You can still use this translation.";
                }

                Result = (selected.Name, directory, selected.Code);
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Download failed: {ex.Message}";
                DownloadButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                TranslationComboBox.IsEnabled = true;
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
    }
}
