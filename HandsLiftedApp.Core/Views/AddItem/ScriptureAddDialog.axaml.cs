using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Importer.Scripture;
using HandsLiftedApp.Importer.Scripture.Models;

namespace HandsLiftedApp.Core.Views
{
    public partial class ScriptureAddDialog : Window
    {
        private sealed class ReferenceState
        {
            public bool IsValid;
            public string? BookCode;
            public string? BookName;
            public int StartChapter;
            public int StartVerse;
            public int EndChapter;
            public int EndVerse;
        }

        // Remembered for the lifetime of the app process only — not a saved user preference.
        private static bool s_preferPickMode;

        private readonly ScriptureLocalUsxStore _store;
        private readonly ReferenceState _state = new();
        private CancellationTokenSource? _validationCts;
        private bool _initializing = true;

        public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }

        public ScriptureAddDialog(ScriptureLocalUsxStore? store = null)
        {
            InitializeComponent();
            _store = store ?? new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath);

            BookComboBox.ItemsSource = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
            BookComboBox.SelectedIndex = 0;

            if (s_preferPickMode)
            {
                PickModeRadio.IsChecked = true;
            }
            else
            {
                TypeModeRadio.IsChecked = true;
            }

            _initializing = false;
        }

        // Avalonia 12's RadioButton/ToggleButton only exposes IsCheckedChanged (no WPF-style
        // Checked/Unchecked events), and it fires twice per group toggle: once when the clicked
        // radio becomes checked while the sibling is still stale-checked, and again when the
        // group manager unchecks the sibling (see SlideThemeDesigner.axaml.cs's
        // PreviewModeToggle_OnChecked for the same pattern). This handler is written as a pure
        // function of both radios' current IsChecked state, so re-running it on both firings is
        // safe: the second, final firing always recomputes and settles on the correct state.
        private void OnModeChanged(object? sender, RoutedEventArgs e)
        {
            if (TypeModeRadio.IsChecked == true)
            {
                TypeModePanel.IsVisible = true;
                PickModePanel.IsVisible = false;

                if (!_initializing && TryReadPickModeValues(out var bookName, out var startChapter, out var startVerse, out var endChapter, out var endVerse))
                {
                    ReferenceTextBox.Text = FormatReference(bookName, startChapter, startVerse, endChapter, endVerse);
                }

                InsertButton.IsEnabled = !_initializing && _state.IsValid;
            }
            else
            {
                TypeModePanel.IsVisible = false;
                PickModePanel.IsVisible = true;

                if (!_initializing && _state.IsValid)
                {
                    var idx = ScriptureBookCatalog.AllBooks.ToList().FindIndex(b => b.Code == _state.BookCode);
                    if (idx >= 0)
                    {
                        BookComboBox.SelectedIndex = idx;
                        StartChapterUpDown.Value = _state.StartChapter;
                        StartVerseUpDown.Value = _state.StartVerse;
                        EndChapterUpDown.Value = _state.EndChapter;
                        EndVerseUpDown.Value = _state.EndVerse;
                    }
                }

                InsertButton.IsEnabled = true;
            }

            if (!_initializing)
            {
                s_preferPickMode = PickModeRadio.IsChecked == true;
            }
        }

        private bool TryReadPickModeValues(out string bookName, out int startChapter, out int startVerse, out int endChapter, out int endVerse)
        {
            bookName = "";
            startChapter = startVerse = endChapter = endVerse = 0;

            if (BookComboBox.SelectedIndex < 0) return false;
            if (StartChapterUpDown.Value is null || StartVerseUpDown.Value is null ||
                EndChapterUpDown.Value is null || EndVerseUpDown.Value is null) return false;

            bookName = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex].Name;
            startChapter = (int)StartChapterUpDown.Value.Value;
            startVerse = (int)StartVerseUpDown.Value.Value;
            endChapter = (int)EndChapterUpDown.Value.Value;
            endVerse = (int)EndVerseUpDown.Value.Value;
            return true;
        }

        private static string FormatReference(string bookName, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            if (startChapter == endChapter && startVerse == endVerse)
            {
                return $"{bookName} {startChapter}:{startVerse}";
            }

            if (startChapter == endChapter)
            {
                return $"{bookName} {startChapter}:{startVerse}-{endVerse}";
            }

            return $"{bookName} {startChapter}:{startVerse}-{endChapter}:{endVerse}";
        }

        private void OnReferenceTextChanged(object? sender, TextChangedEventArgs e)
        {
            _validationCts?.Cancel();
            var cts = new CancellationTokenSource();
            _validationCts = cts;
            _ = ValidateTypedReferenceAsync(ReferenceTextBox.Text ?? "", cts.Token);
        }

        private async Task ValidateTypedReferenceAsync(string text, CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!ScriptureReferenceParser.TryParse(text, out var parsed, out var parseError))
            {
                SetInvalid(parseError!);
                return;
            }

            ScriptureBook book;
            try
            {
                SetChecking(parsed.BookName);
                book = await _store.LoadBookAsync(parsed.BookCode);
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested) return;
                SetInvalid($"Couldn't load {parsed.BookName} — check scripture data path.");
                return;
            }

            if (token.IsCancellationRequested) return;

            var verses = book.Paragraphs.SelectMany(p => p.Verses).ToList();
            var chapters = verses.Select(v => v.Chapter).ToHashSet();

            if (!chapters.Contains(parsed.StartChapter) || (parsed.EndVerse is not null && !chapters.Contains(parsed.EndChapter)))
            {
                SetInvalid($"{parsed.BookName} has {chapters.Max()} chapters.");
                return;
            }

            var startChapterVerses = verses.Where(v => v.Chapter == parsed.StartChapter).Select(v => v.VerseNumber).ToList();
            var maxStartVerse = startChapterVerses.Max();

            if (parsed.StartVerse is not null && !startChapterVerses.Contains(parsed.StartVerse.Value))
            {
                SetInvalid($"{parsed.BookName} {parsed.StartChapter} has {maxStartVerse} verses.");
                return;
            }

            int resolvedStartVerse = parsed.StartVerse ?? 1;
            int resolvedEndVerse;

            if (parsed.EndVerse is null)
            {
                resolvedEndVerse = maxStartVerse;
            }
            else
            {
                var endChapterVerses = parsed.EndChapter == parsed.StartChapter
                    ? startChapterVerses
                    : verses.Where(v => v.Chapter == parsed.EndChapter).Select(v => v.VerseNumber).ToList();
                var maxEndVerse = endChapterVerses.Max();

                if (!endChapterVerses.Contains(parsed.EndVerse.Value))
                {
                    SetInvalid($"{parsed.BookName} {parsed.EndChapter} has {maxEndVerse} verses.");
                    return;
                }

                resolvedEndVerse = parsed.EndVerse.Value;
            }

            SetValid(parsed.BookCode, parsed.BookName, parsed.StartChapter, resolvedStartVerse, parsed.EndChapter, resolvedEndVerse);
        }

        private void SetChecking(string bookName)
        {
            ReferenceHintText.Text = $"Checking {bookName}…";
            ReferenceHintText.Foreground = Brushes.Gray;
        }

        private void SetInvalid(string message)
        {
            _state.IsValid = false;
            ReferenceHintText.Text = message;
            ReferenceHintText.Foreground = Brushes.IndianRed;
            InsertButton.IsEnabled = false;
        }

        private void SetValid(string bookCode, string bookName, int startChapter, int startVerse, int endChapter, int endVerse)
        {
            _state.IsValid = true;
            _state.BookCode = bookCode;
            _state.BookName = bookName;
            _state.StartChapter = startChapter;
            _state.StartVerse = startVerse;
            _state.EndChapter = endChapter;
            _state.EndVerse = endVerse;

            ReferenceHintText.Text = "";
            InsertButton.IsEnabled = true;
        }

        private void OnConfirmInsert(object? sender, RoutedEventArgs e)
        {
            if (PickModeRadio.IsChecked == true)
            {
                if (!TryReadPickModeValues(out var bookName, out var startChapter, out var startVerse, out var endChapter, out var endVerse)) return;
                var selected = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex];
                Result = (selected.Code, bookName, startChapter, startVerse, endChapter, endVerse);
                Close();
                return;
            }

            if (!_state.IsValid) return;
            Result = (_state.BookCode!, _state.BookName!, _state.StartChapter, _state.StartVerse, _state.EndChapter, _state.EndVerse);
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
