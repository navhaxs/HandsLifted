using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views.Editors.Song;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Controls
{
    public partial class ItemSlidesView : UserControl
    {
        public ItemSlidesView()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                //this.DataContext = new SectionHeadingItem<ItemStateImpl>();
                //this.DataContext = PlaylistUtils.CreateSong();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: if follow mode enabled
            // (and have UI to "recentre" like in google maps)
            //MessageBus.Current.SendMessage(new FocusSelectedItem());


            // TODO: 'on click' event - NOT just 'on clicked AND index has changed'
            // https://github.com/AvaloniaUI/Avalonia/discussions/7182
            //MessageBus.Current.SendMessage(new OnSelectionClickedMessage());
        }

        internal class OnSelectionClickedMessage
        {
            //public Item<ItemStateImpl> SourceItem { get; set; }
        }

        private void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SongItemInstance item })
            {
                SongEditorViewModel songEditorViewModel = new SongEditorViewModel(item, Globals.Instance.MainViewModel.Playlist);
                // songEditorViewModel.SongDataUpdated += (ex, ey) =>
                // {
                //             
                // };
                SingleSongEditorWindow songEditorWindow = new SingleSongEditorWindow() { DataContext = songEditorViewModel };
                songEditorWindow.Show();
                return;
            }
            if (sender is Control { DataContext: MediaGroupItemInstance mediaGroupItemInstance })
            {
                GenericContentEditorWindow songEditorWindow = new GenericContentEditorWindow () { DataContext = mediaGroupItemInstance };
                songEditorWindow.Show();
                return;
            }
        }
        
        private void MoveUpItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                MessageBus.Current.SendMessage(new MoveItemCommand() {SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.UP });
            }
        }
        private void MoveDownItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                MessageBus.Current.SendMessage(new MoveItemCommand() {SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.DOWN });
            }
        }
        private void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                // TODO confirmation window
                
                MessageBus.Current.SendMessage(new MoveItemCommand() {SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.REMOVE });
            }
        }

        private void ItemBorder_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ((Border)sender).Classes.Add("fade-in");
        }
    }
}
