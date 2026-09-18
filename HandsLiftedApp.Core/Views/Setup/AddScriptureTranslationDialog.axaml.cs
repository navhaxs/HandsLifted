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
            DirectoryTextBox.Text = Path.Combine(Constants.APP_DATA_DIR, "ScriptureData", selected.Abbrev);
        }

        private void OnDirectoryTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Any programmatic set below is followed immediately by this handler firing too -
            // there's no way to distinguish "user typed" from "we just set it" via this event
            // alone, so OnTranslationSelectionChanged only auto-fills when nothing has been typed
            // since the dialog opened, and this flag latches true on the very first change,
            // whichever caused it. That means picking a different translation immediately after
            // opening the dialog (before touching the folder box) still auto-fills correctly -
            // the first TextChanged is this class's own initial assignment - but AFTER the user's
            // very first manual edit, subsequent translation picks stop overwriting their choice.
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
