using Avalonia.Controls;

namespace HandsLiftedApp.Controls
{
    public partial class EditableLabelControl : UserControl
    {
        public EditableLabelControl()
        {
            InitializeComponent();

            thisTextBlock.PointerPressed += ThisTextBlock_PointerPressed;
            thisTextBox.LostFocus += ThisTextBox_LostFocus;
        }

        private void ThisTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            thisTextBox.IsVisible = false;
        }

        private void ThisTextBlock_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            thisTextBox.IsVisible = true;
        }
    }
}
