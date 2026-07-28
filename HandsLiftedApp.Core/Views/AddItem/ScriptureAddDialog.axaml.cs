using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.Views
{
    public partial class ScriptureAddDialog : Window
    {
        public (string BookCode, string BookName, int StartChapter, int StartVerse, int EndChapter, int EndVerse)? Result { get; private set; }

        public ScriptureAddDialog()
        {
            InitializeComponent();
            BookComboBox.ItemsSource = ScriptureBookCatalog.AllBooks.Select(b => b.Name).ToList();
            BookComboBox.SelectedIndex = 0;
        }

        private void OnConfirmInsert(object? sender, RoutedEventArgs e)
        {
            if (BookComboBox.SelectedIndex < 0) return;
            if (StartChapterUpDown.Value is null || StartVerseUpDown.Value is null ||
                EndChapterUpDown.Value is null || EndVerseUpDown.Value is null) return;

            var selected = ScriptureBookCatalog.AllBooks[BookComboBox.SelectedIndex];

            Result = (
                selected.Code,
                selected.Name,
                (int)StartChapterUpDown.Value.Value,
                (int)StartVerseUpDown.Value.Value,
                (int)EndChapterUpDown.Value.Value,
                (int)EndVerseUpDown.Value.Value
            );
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
