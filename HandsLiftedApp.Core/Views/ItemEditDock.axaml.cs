using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.Views
{
    public partial class ItemEditDock : UserControl
    {
        public ItemEditDock()
        {
            InitializeComponent();
        }
        
        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is PowerPointPresentationItemInstance instance)
            {
                instance._Sync();
            }
        }
    }
}