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
        private int selectedIndex;
        private ItemState selectedItem;

        public PlaylistState(Playlist playlist)
        {
            Playlist = playlist;
        }
        public Playlist? Playlist { get; set; }

        public int SelectedIndex
        {
            get => selectedIndex; set
            {
                this.RaiseAndSetIfChanged(ref selectedIndex, value);
                OnPropertyChanged();
            }
        }

        public ItemState SelectedItem { get => selectedItem; set
            {
                this.RaiseAndSetIfChanged(ref selectedItem, value);
                OnPropertyChanged();
            }
        }

        List<ItemState> ItemStates => Playlist.Items.Select((item, index) => convertDataToState(item, index)).ToList();

        // slide index here?

        ItemState convertDataToState(Item item, int index) => new ItemState() { Item = item, Index = index };

    }
}
