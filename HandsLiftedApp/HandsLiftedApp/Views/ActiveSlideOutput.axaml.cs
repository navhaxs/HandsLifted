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



            t = new System.Timers.Timer();
            t.AutoReset = false;
            t.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
            t.Interval = 1000;
            t.Start();
        }

        void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine(DateTime.Now.ToString("o"));

            //Dispatcher.UIThread.InvokeAsync(() =>
            //{
            //    TransitioningContentControl root = this.FindControl<TransitioningContentControl>("root");
            //    root.Content = DateTime.Now.ToString("o");
            //});

            t.Start();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
