using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels.Editor;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;

namespace HandsLiftedApp.Controls
{
    public partial class SongArrangementControl : UserControl
    {
        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        public SongArrangementControl()
        {
            InitializeComponent();

            EditCommand = ReactiveCommand.Create(RunTheThing);
        }
        void RunTheThing()
        {
            Debug.Print("a");
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
        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            //(sender as Button)!.Content = "Ginger";
            //(sender as Button)!.ContextMenu!.Open(null);
        }
        public void BtnConvoContact_Click(object? sender, RoutedEventArgs args)
        {
            //(sender as Button)!.Content = "Ginger";
            //(sender as Button)!.ContextMenu!.Open(null);
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            //(sender as Button)!.Content = "Ginger";
            var stanza = (Data.Models.Items.SongStanza)((Control)sender).DataContext;
            var m = new Data.Models.Items.SongItem<Models.SlideState.SongTitleSlideStateImpl, Models.SlideState.SongSlideStateImpl, Models.ItemStateImpl>.Ref<Data.Models.Items.SongStanza>() { Value = stanza };
            ((Data.Models.Items.SongItem<Models.SlideState.SongTitleSlideStateImpl, Models.SlideState.SongSlideStateImpl, Models.ItemStateImpl>)this.DataContext).Arrangement.Add(m);
        }

    }
}
