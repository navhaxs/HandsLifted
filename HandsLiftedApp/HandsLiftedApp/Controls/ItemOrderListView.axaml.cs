using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Controls
{
    public partial class ItemOrderListView : UserControl
    {
        public ItemOrderListView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
