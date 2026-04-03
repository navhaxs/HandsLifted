using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views
{
    public partial class RestoreAutosaveConfirmationWindow : Window
    {
        public bool Result = false;
        
        public RestoreAutosaveConfirmationWindow()
        {
            InitializeComponent();
        }

        private void OnConfirmRestore(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void OnIgnore(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}