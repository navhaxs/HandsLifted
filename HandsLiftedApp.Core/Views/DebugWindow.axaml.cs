using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Core.Views
{
    public partial class DebugWindow : Window
    {
        public DebugWindow()
        {
            InitializeComponent();
        }

        private void DebugClick(object? sender, RoutedEventArgs e)
        {
            Debugger.Launch();
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}