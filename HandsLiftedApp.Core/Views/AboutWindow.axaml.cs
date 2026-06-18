using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Core.Views
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

            var buttonNotices = this.FindControl<Button>("buttonNotices");
            buttonNotices!.Click += async (_, _) =>
                await new ThirdPartyNoticesWindow().ShowDialog(this);

            this.DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        public String BuildDateTime { get { return BuildInfo.Version.GetBuildDateTime(); } }
        public String GitHash { get { return BuildInfo.Version.GetGitHash(); } }

    }
}
