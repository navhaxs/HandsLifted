using Avalonia.Controls;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
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

            _activeItemSlide = this.WhenAnyValue(x => x.SelectedItem.State.SelectedSlide,
                    (Slide selectedSlide) => selectedSlide)
                    .ToProperty(this, c => c.ActiveItemSlide);

            if (Design.IsDesignMode)
            {
                SelectedIndex = 0;
            }
        }

        public string _playlistWorkingDirectory = @"C:\VisionScreens\TestPlaylist";
        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                this.RaiseAndSetIfChanged(ref selectedIndex, value);
            }
        }

        private ObservableAsPropertyHelper<Item<ItemStateImpl>> _selectedItem;
        public Item<ItemStateImpl> SelectedItem { get => _selectedItem.Value; }

        private ObservableAsPropertyHelper<Slide> _activeItemSlide;
        public Slide ActiveItemSlide { get => _activeItemSlide.Value; }

        // TODO: "Presentation State" can be moved out of playlist state.
        private bool _isLogo = false;
        public bool IsLogo { get => _isLogo; set => this.RaiseAndSetIfChanged(ref _isLogo, value); }

        private bool _isBlank = false;
        public bool IsBlank { get => _isBlank; set => this.RaiseAndSetIfChanged(ref _isBlank, value); }

        private bool _isFreeze = false;
        public bool IsFreeze { get => _isFreeze; set => this.RaiseAndSetIfChanged(ref _isFreeze, value); }

    }
}
