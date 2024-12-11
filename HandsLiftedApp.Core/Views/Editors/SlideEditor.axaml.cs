using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class SlideEditor : UserControl
    {
        public SlideEditor()
        {
            InitializeComponent();

            TestWindow1 tw1 = new();
            tw1.Show();
        }
    }
}