using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

namespace HandsLiftedApp.Views
{
    public partial class ActiveSlideOutput : UserControl
    {
        System.Timers.Timer t;

        public ActiveSlideOutput()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
