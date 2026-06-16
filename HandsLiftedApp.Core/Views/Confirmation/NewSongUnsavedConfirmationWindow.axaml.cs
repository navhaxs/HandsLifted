using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views.Confirmation
{
    public partial class NewSongUnsavedConfirmationWindow : Window
    {
        public enum DialogResult { Save, Discard, Cancel }

        public DialogResult Result = DialogResult.Cancel;

        public NewSongUnsavedConfirmationWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void OnConfirmSave(object? sender, RoutedEventArgs e)
        {
            Result = DialogResult.Save;
            Close();
        }

        private void OnConfirmDiscard(object? sender, RoutedEventArgs e)
        {
            Result = DialogResult.Discard;
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
