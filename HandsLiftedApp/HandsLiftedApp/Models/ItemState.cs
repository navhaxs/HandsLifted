using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
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
        private SlideStateBase selectedItem;

        public int Index { get; set; }

        public Item? Item { get; set; }

        public List<SlideStateBase> SlideStates => Item.Slides.Select((s, index) => convertDataToState(s, index)).ToList();


        // slide index here?
        public int SelectedIndex
        {
            get => selectedIndex; set
            {
                selectedIndex = value;

                if (selectedIndex > -1)
                {
                MessageBus.Current.SendMessage(new Class1() { SourceItemStateIndex = Index });

                }
            }
        }

        public SlideStateBase SelectedItem { get => selectedItem; set
            {
                this.RaiseAndSetIfChanged(ref selectedItem, value);
                /*OnPropertyChanged()*/;
            }
        }

        SlideStateBase convertDataToState(Slide slide, int index)
        {
            switch (slide)
            {
                case SongSlide d:
                    return new SongSlideState(d, index);
                case SongTitleSlide d:
                    return new SongTitleSlideState(d, index);
                case VideoSlide d:
                    return new VideoSlideState(d, index);
                case ImageSlide d:
                    return new ImageSlideState(d, index);
                default:
                    throw new Exception("error");
                    break;
            }
        }

    }
}
