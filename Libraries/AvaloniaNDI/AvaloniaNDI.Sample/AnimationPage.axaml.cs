using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvaloniaNDI.Sample
{
    public partial class AnimationPage : UserControl
    {
        public AnimationPage()
        {
            InitializeComponent();

            this.DataContext = new AnimationsPageViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ToggleClock(object sender, RoutedEventArgs args)
        {
            //var button = sender as Button;
            //var clock = button.Clock;

            //if (clock.PlayState == PlayState.Run)
            //{
            //    clock.PlayState = PlayState.Pause;
            //}
            //else if (clock.PlayState == PlayState.Pause)
            //{
            //    clock.PlayState = PlayState.Run;
            //}
        }
    }
}
