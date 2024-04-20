using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class AutoAdvanceTimerControlViewWrapper : UserControl
    {
        public AutoAdvanceTimerControlViewWrapper()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ResumeButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is AutoAdvanceTimerController controller)
            {
                controller.Timer?.Resume();
            }
        }

        private void PauseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is AutoAdvanceTimerController controller)
            {
                controller.Timer?.Stop();
            }
        }
    }
}
