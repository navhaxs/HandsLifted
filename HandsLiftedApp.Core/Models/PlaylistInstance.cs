using DynamicData.Binding;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.Models
{
    public class PlaylistInstance : ReactiveObject
    {
        private string _playlistWorkingDirectory = @"C:\VisionScreens\TestPlaylist";

        public PlaylistInstance()
        {
            _selectedItem = Observable.CombineLatest(
                this.WhenAnyValue(x => x.SelectedItemIndex),
                Playlist.Items.ObserveCollectionChanges(),
                (selectedIndex, items) =>
                {
                    if (selectedIndex != -1)
                    {
                        return Playlist.Items.ElementAtOrDefault(selectedIndex);
                    }

                    return new BlankItem();
                }
            ).ToProperty(this, x => x.SelectedItem);

        }

        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int _selectedIndex = -1;
        public int SelectedItemIndex
        {
            get { return _selectedIndex; }
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedIndex, value);
            }
        }

        private readonly ObservableAsPropertyHelper<Item?> _selectedItem;
        public Item? SelectedItem { get => _selectedItem.Value; }

        private Playlist _playlist = new Playlist();
        public Playlist Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }

    }
}
