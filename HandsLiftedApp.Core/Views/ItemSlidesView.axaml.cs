using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;

//using HandsLiftedApp.Data.Models.Items;
//using HandsLiftedApp.Models.ItemState;
//using HandsLiftedApp.Utils;

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
                SongEditorViewModel songEditorViewModel = new SongEditorViewModel() { Song = item, Playlist = Globals.MainViewModel.Playlist };
                songEditorViewModel.SongDataUpdated += (ex, ey) =>
                {
                            
                };
                SongEditorWindow songEditorWindow = new SongEditorWindow() { DataContext = songEditorViewModel };
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
    }
}
