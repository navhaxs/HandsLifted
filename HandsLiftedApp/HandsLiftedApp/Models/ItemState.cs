using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.Render;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models
{
    // Move into PlaylistState
    public class ItemState : ViewModelBase
    {
        private int selectedIndex;
        private SlideState selectedItem;

        public int Index { get; set; }

        public Item? Item { get; set; }

        public List<SlideState> SlideStates => Item.Slides.Select(s => convertDataToState(s)).ToList();


        // slide index here?
        public int SelectedIndex
        {
            get => selectedIndex; set
            {
                selectedIndex = value;
                MessageBus.Current.SendMessage(new Class1() { SourceItemStateIndex = Index });
            }
        }

        public SlideState SelectedItem { get => selectedItem; set
            {
                this.RaiseAndSetIfChanged(ref selectedItem, value);
                OnPropertyChanged();
            }
        }

        SlideState convertDataToState(Slide slide)
        {
            switch (slide)
            {
                case SongSlide d:
                    return new SongSlideState(d);
                case VideoSlide d:
                    return new VideoSlideState(d);
                case ImageSlide d:
                    return new ImageSlideState(d);
                default:
                    throw new Exception("error");
                    break;
            }
        }

    }
}
