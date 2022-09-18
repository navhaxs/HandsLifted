using Avalonia.Controls;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models
{
    [XmlRoot("Playlist", Namespace = Constants.Namespace, IsNullable = false)]
    // todo: see https://stackoverflow.com/questions/11886290/use-the-xmlinclude-or-soapinclude-attribute-to-specify-types-that-are-not-known
    [XmlInclude(typeof(SongItem<ISongTitleSlideState, ISongSlideState, IItemState>))]
    [XmlInclude(typeof(SlidesGroupItem<IItemState, IItemAutoAdvanceTimerState>))]
    [Serializable]
    public class Playlist<T, I> : ReactiveObject where T : IPlaylistState where I : IItemState
    {

        public SerializableDictionary<String, String> Meta { get; set; } = new SerializableDictionary<String, String>();

        private T _state;

        [XmlIgnore]
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        private ObservableCollection<Item<I>> _items = new ObservableCollection<Item<I>>();

        public Playlist()
        {
            if (Design.IsDesignMode)
                return;

            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public ObservableCollection<Item<I>> Items { get => _items; set => this.RaiseAndSetIfChanged(ref _items, value); }
    }
}
