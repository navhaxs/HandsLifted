﻿using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models
{
    [XmlRoot("Playlist", Namespace = Constants.Namespace, IsNullable = false)]
    // todo: see https://stackoverflow.com/questions/11886290/use-the-xmlinclude-or-soapinclude-attribute-to-specify-types-that-are-not-known
    [XmlInclude(typeof(SongItem<ISongTitleSlideState, ISongSlideState, IItemState>))]
    [XmlInclude(typeof(SlidesGroup<IItemState>))]
    [Serializable]
    public class Playlist<T, I> : ReactiveObject where T : IPlaylistState where I : IItemState
    {

        public SerializableDictionary<String, String> Meta { get; set; } = new SerializableDictionary<String, String>();

        private T _state;
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        private ObservableCollection<Item<I>> _items = new ObservableCollection<Item<I>>();

        public Playlist()
        {
            State = (T)Activator.CreateInstance(typeof(T), this);
            Items.CollectionChanged += Items_CollectionChanged;
        }

        // update the item states
        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach (var (item, index) in Items.Select((item, index) => (item, index)))
            {
                item.State.ItemIndex = index;
            };
        }

        public ObservableCollection<Item<I>> Items { get => _items; set => this.RaiseAndSetIfChanged(ref _items, value); }
    }
}
