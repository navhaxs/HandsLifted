using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Windows;

namespace HandsLiftedApp.Controls
{
    public partial class ItemStateView : UserControl
    {
        public ItemStateView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
