using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Item
{
    public partial class SongItemInfo : UserControl
    {
        public SongItemInfo()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
