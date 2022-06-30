using DynamicData;
using DynamicData.Binding;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
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
    public class PlaylistState : ViewModelBase
    {

        public PlaylistState(Playlist playlist)
        {
            Playlist = playlist;

            //_itemStates = this.WhenAnyValue(x => x.Playlist.Items, (items) =>
            //{
            //    if (items == null)
            //    {
            //        return new ObservableCollection<ItemState>();
            //    }

            //    return new ObservableCollection<ItemState>(items.Select((item, index) => convertDataToState(item, index)).ToList()); // convertDataToState
            //})
            //    .ToProperty(this, x => x.ItemStates);
            //;

            _itemStates = this.Playlist.Items
                // Convert the collection to a stream of chunks,
                // so we have IObservable<IChangeSet<TKey, TValue>>
                // type also known as the DynamicData monad.
                .ToObservableChangeSet(x => x)
                // Each time the collection changes, we get
                // all updated items at once.
                .ToCollection()
                .Select((collection) => new ObservableCollection<ItemState>(collection.Select((item, index) => convertDataToState(item, index))))
                .ToProperty(this, x => x.ItemStates);
            ;

            _selectedItem = this.WhenAnyValue(x => x.SelectedIndex, x => x.ItemStates, (selectedIndex, itemStates) =>
                {
                    if (selectedIndex != -1)
                        return itemStates[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedItem);

        }


        public Playlist _playlist = new Playlist();
        public Playlist Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }


        private int selectedIndex = -1;
        public int SelectedIndex { get => selectedIndex; set => this.RaiseAndSetIfChanged(ref selectedIndex, value); }


        private ObservableAsPropertyHelper<ItemState> _selectedItem;
        public ItemState SelectedItem { get => _selectedItem.Value; }

        private ObservableAsPropertyHelper<ObservableCollection<ItemState>> _itemStates;
        public ObservableCollection<ItemState> ItemStates { get => _itemStates.Value; }

        // slide index here?

        ItemState convertDataToState(Item item, int index) => new ItemState() { Item = item, Index = index };

    }
}
