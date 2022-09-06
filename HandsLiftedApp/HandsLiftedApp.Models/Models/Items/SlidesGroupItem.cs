using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    public class SlidesGroupItem<I> : Item<I> where I : IItemState
    {

        private ObservableCollection<Slide> _internal_slides = new ObservableCollection<Slide>();

        public SlidesGroupItem()
        {
            _Slides.CollectionChanged += (s, x) =>
            {
                this.RaisePropertyChanged(nameof(Slides));
            };
        }

        [XmlIgnore] // TODO
        public ObservableCollection<Slide> _Slides { get => _internal_slides; set => this.RaiseAndSetIfChanged(ref _internal_slides, value); }
        [XmlIgnore]
        public override ObservableCollection<Slide> Slides { get => _Slides; }


    }
}
