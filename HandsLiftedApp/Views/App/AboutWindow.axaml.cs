using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace HandsLiftedApp.Views.App
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            var buttonDone = this.FindControl<Button>("buttonDone");
            buttonDone.Click += (o, e) => this.Close();

            this.DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public String BuildDate { get { return BuildInfo.Version.getBuildDate(); } }
        public String GitHash { get { return BuildInfo.Version.getGitHash(); } }

    }
}
