using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    public partial class ImportGoogleSlidesWindow : Window
    {
        public string? ImportId { get; set; } = "1YkvdLUPlzVnWwEaO3afqVHbBqlzm7tSk7YZsEzyy8xM";

        private bool _isImportButtonClicked = false;

        public ImportGoogleSlidesWindow()
        {
            InitializeComponent();

            DataContext = this;

            Closing += (s, e) =>
            {
                if (!_isImportButtonClicked)
                {
                    ImportId = null;
                }
            };
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            _isImportButtonClicked = true;
            Close();
        }
    }
}