using Avalonia.Controls;
using HandsLiftedApp.Controls;

namespace HandsLiftedApp.Core.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            
            Win10DropshadowWorkaround.Register(this);
        }
    }
}