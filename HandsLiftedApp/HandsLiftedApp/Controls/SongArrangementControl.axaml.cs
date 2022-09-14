using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
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
            var stanza = (SongStanza)((Control)sender).DataContext;
            var m = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<Data.Models.Items.SongStanza>() { Value = stanza };
            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Add(m);
        }

        //insert clone
        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> stanza = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>)((Control)sender).DataContext;

            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> clonedStanza = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> { Value = stanza.Value };

            var lastIndex = ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.IndexOf(stanza);

            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Insert(lastIndex + 1, clonedStanza);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> stanza = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>)((Control)sender).DataContext;

            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Remove(stanza);
        }


    }
}
