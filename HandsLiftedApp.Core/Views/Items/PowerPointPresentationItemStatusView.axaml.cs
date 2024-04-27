using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.Views.Items
{
    public partial class PowerPointPresentationItemStatusView : UserControl
    {
        public PowerPointPresentationItemStatusView()
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