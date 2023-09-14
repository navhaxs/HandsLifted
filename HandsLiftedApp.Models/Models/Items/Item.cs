using Avalonia.Animation;
using Avalonia.Media;
using HandsLiftedApp.Data.Models.Types;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Item", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public abstract class Item<T> : ReactiveObject, IDisposable where T : IItemState
    {
        [XmlIgnore]
        public Guid Uuid { get; set; }

        private T _state;
        [XmlIgnore]
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        protected Item()
        {
            Uuid = Guid.NewGuid();
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        private string _title = "New Item";
        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }


        public XmlColor _itemGroupColour = Color.Parse("#3a344a");
        [XmlIgnore]
        public Color ItemGroupColour { get => _itemGroupColour; set => this.RaiseAndSetIfChanged(ref _itemGroupColour, value); }

        [XmlIgnore]
        public abstract ObservableCollection<Slide?> Slides { get; }

        public void Dispose()
        {
        }
    }

    public interface IItemState
    {
        public int ItemIndex { get; set; }
        public int SelectedSlideIndex { get; set; }

        public IPageTransition? PageTransition { get; set; }

        //public Slide GenerateSlideFromSource(string filename); //, int index);
    }
}
