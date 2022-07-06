using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models
{
    public class PlaylistStateImpl : ViewModelBase, IPlaylistState
    {
        Playlist<PlaylistStateImpl, ItemStateImpl> parent;
        public PlaylistStateImpl(ref Playlist<PlaylistStateImpl, ItemStateImpl> playlist)
        {
            parent = playlist;

            _selectedItem = this.WhenAnyValue(x => x.SelectedIndex, (selectedIndex) =>
                {
                    if (selectedIndex != -1)
                        return parent.Items[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedItem);

            //parent.Items.CollectionChanged += Items_CollectionChanged;

            //UpdateItemStates();

            if (Design.IsDesignMode) {
                SelectedIndex = 0;
            }
        }

        //void UpdateItemStates()
        //{
        //    var x = new ObservableCollection<ItemStateImpl>(parent.Items.Select((item, index) => convertDataToState(item, index)).ToList());
        //    _itemStates.UpdateCollection(x);
        //}

        //private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        //{
        //    UpdateItemStates();
        //}

        //public Playlist<PlaylistStateImpl> _playlist = new Playlist<PlaylistStateImpl>();
        //public Playlist<PlaylistStateImpl> Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }


        public string _playlistWorkingDirectory = @"C:\VisionScreens\TestPlaylist";
        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int selectedIndex = -1;
        public int SelectedIndex { get => selectedIndex; set {
                this.RaiseAndSetIfChanged(ref selectedIndex, value);
                //OnSelectedIndexChanged();
            }
        }

        private ObservableAsPropertyHelper<Item<ItemStateImpl>> _selectedItem;
        public Item<ItemStateImpl> SelectedItem { get => _selectedItem.Value; }


        // slide index here?

        //ItemStateImpl convertDataToState(Item item, int index) => new ItemStateImpl() { Item = item, Index = index };

        void OnSelectedIndexChanged()
        {
            foreach (var (item, index) in parent.Items.WithIndex())
            {
                if (SelectedIndex != index)
                    item.State.SelectedIndex = -1;
            }
        }
    }
}
