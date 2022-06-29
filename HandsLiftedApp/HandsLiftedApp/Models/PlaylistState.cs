using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models
{
    public class PlaylistState : ViewModelBase
    {

        public PlaylistState(Playlist playlist)
        {
            Playlist = playlist;

            _selectedItem = this.WhenAnyValue(x => x.SelectedIndex, x => x.ItemStates, (selectedIndex, itemStates) =>
                {
                    if (selectedIndex != -1)
                        return itemStates[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedItem);
        }


        public Playlist? _playlist;
        public Playlist? Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }


        private int selectedIndex = -1;
        public int SelectedIndex { get => selectedIndex; set => this.RaiseAndSetIfChanged(ref selectedIndex, value); }


        private ObservableAsPropertyHelper<ItemState> _selectedItem;
        public ItemState SelectedItem { get => _selectedItem.Value; }

        public List<ItemState> ItemStates => Playlist.Items.Select((item, index) => convertDataToState(item, index)).ToList();

        // slide index here?

        ItemState convertDataToState(Item item, int index) => new ItemState() { Item = item, Index = index };

    }
}
