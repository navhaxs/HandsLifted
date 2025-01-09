using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Core.Views
{
    public partial class ThisIsATestBuildWarningWindow : Window
    {
        public ThisIsATestBuildWarningWindow()
        {
            InitializeComponent();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}