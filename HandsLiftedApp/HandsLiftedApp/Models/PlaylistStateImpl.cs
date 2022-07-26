using Avalonia.Controls;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models
{
    public class PlaylistStateImpl : ViewModelBase, IPlaylistState
    {
        Playlist<PlaylistStateImpl, ItemStateImpl> Playlist;
        public PlaylistStateImpl(ref Playlist<PlaylistStateImpl, ItemStateImpl> playlist)
        {
            Playlist = playlist;

            MessageBus.Current.Listen<ActiveSlideChangedMessage>()
                .Subscribe(x =>
                {
                    var lastSelectedIndex = Playlist.State.SelectedIndex;

                    var sourceIndex = Playlist.Items.IndexOf(x.SourceItem);


                    // update the selected item in the playlist
                    Playlist.State.SelectedIndex = sourceIndex;


                    // notify the last deselected item in the playlist
                    if (lastSelectedIndex > -1 && sourceIndex != lastSelectedIndex && Playlist != null)
                    {
                        Playlist.Items[lastSelectedIndex].State.SelectedIndex = -1;
                    }
                });

            _selectedItem = this.WhenAnyValue(x => x.SelectedIndex, (selectedIndex) =>
                {
                    if (selectedIndex != -1)
                        return Playlist.Items[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedItem);

            if (Design.IsDesignMode) {
                SelectedIndex = 0;
            }
        }

        public string _playlistWorkingDirectory = @"C:\VisionScreens\TestPlaylist";
        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int selectedIndex = -1;
        public int SelectedIndex { get => selectedIndex; set => this.RaiseAndSetIfChanged(ref selectedIndex, value); }

        private ObservableAsPropertyHelper<Item<ItemStateImpl>> _selectedItem;
        public Item<ItemStateImpl> SelectedItem { get => _selectedItem.Value; }
        void OnSelectedIndexChanged()
        {
            foreach (var (item, index) in Playlist.Items.WithIndex())
            {
                if (SelectedIndex != index)
                    item.State.SelectedIndex = -1;
            }
        }
    }
}
