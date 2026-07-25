using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views.Confirmation
{
    public partial class DeleteConfirmationWindow : Window
    {
        public bool Confirmed { get; private set; }

        public DeleteConfirmationWindow(string itemName)
        {
            InitializeComponent();
            MessageText.Text = $"Are you sure you want to delete \"{itemName}\"? This cannot be undone.";
        }

        private void OnConfirmDelete(object? sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
