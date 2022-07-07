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

            //ListBox listBox = this.FindControl<ListBox>("itemsListBox");
            //listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
