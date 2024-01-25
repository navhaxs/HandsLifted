using DynamicData.Binding;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.Models
{
    public class PlaylistInstance : ReactiveObject
    {
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

        private DateTime? _lastSaved;
        public DateTime? LastSaved { get => _lastSaved; set => this.RaiseAndSetIfChanged(ref _lastSaved, value); }

        private string _playlistFilePath = @"D:\VisionScreensCore-TestData\test.xml";
        public string PlaylistFilePath { get => _playlistFilePath; set => this.RaiseAndSetIfChanged(ref _playlistFilePath, value); }

        private string _playlistWorkingDirectory = @"D:\VisionScreensCore-TestData\_Data";
        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int _selectedIndex = -1;
        public int SelectedItemIndex
        {
            get => _selectedIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
        }

        private readonly ObservableAsPropertyHelper<Item?> _selectedItem;
        public Item? SelectedItem { get => _selectedItem.Value; }

        private Playlist _playlist = new();
        public Playlist Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }

    }
}
