using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels.Editor;

namespace HandsLiftedApp.Controls
{
    public partial class SongArrangementControl : UserControl
    {
        public SongArrangementControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void X_Click(object? sender, RoutedEventArgs e)
        {
            (sender as Button)!.Content = "Ginger";
        }

        public void btn_OnClick(object? sender, RoutedEventArgs args)
        {
            (sender as Button)!.Content = "Ginger";
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            //(sender as Button)!.Content = "Ginger";
            var stanza = (Data.Models.Items.SongStanza)((Control)sender).DataContext;
            var m = new Data.Models.Items.SongItem<Models.SlideState.SongTitleSlideStateImpl, Models.SlideState.SongSlideStateImpl, Models.ItemStateImpl>.Ref<Data.Models.Items.SongStanza>() { Value = stanza };
            ((SongEditorViewModel)this.DataContext).song.Arrangement.Add(m);
        }

    }
}
