using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Windows;

namespace HandsLiftedApp.Controls
{
    public partial class ItemStateView : UserControl
    {
        ListBox listBox;

        public ItemStateView()
        {
            InitializeComponent();

            listBox = this.FindControl<ListBox>("List");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
