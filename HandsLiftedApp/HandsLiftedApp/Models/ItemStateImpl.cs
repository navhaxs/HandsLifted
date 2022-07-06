using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views.Editor;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using static HandsLiftedApp.ViewModels.Editor.SongEditorViewModel;

namespace HandsLiftedApp.Models
{
    // Move into PlaylistState
    public class ItemStateImpl : ViewModelBase, IItemState
    {
        private Item<ItemStateImpl> parent;

        //private SlideStateBase selectedItem;

        public ItemStateImpl(ref Item<ItemStateImpl> item)
        {
            parent = item;
            EditCommand = ReactiveCommand.Create(RunTheThing);

            _selectedSlide = this.WhenAnyValue(x => x.SelectedIndex, (selectedIndex) =>
            {
                if (selectedIndex != -1 && parent.Slides != null && parent.Slides.Count > selectedIndex)
                    return parent.Slides[selectedIndex];

                return null;
            })
            .ToProperty(this, x => x.SelectedSlide);

        }

        //public int Index { get; set; }

        //private Item<ItemStateImpl> _item;
        //public Item<ItemStateImpl> Item { get => _item; set => this.RaiseAndSetIfChanged(ref _item, value); }
        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex; set
            {
                this.RaiseAndSetIfChanged(ref _selectedIndex, value);

                if (_selectedIndex > -1)
                {
                    MessageBus.Current.SendMessage(new ActiveSlideChangedMessage() { SourceItemStateIndex = ItemIndex });
                }
            }
        }


        private ObservableAsPropertyHelper<Slide> _selectedSlide;
        public Slide SelectedSlide { get => _selectedSlide.Value; }


        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        private int _ItemIndex;
        public int ItemIndex { get => _ItemIndex; set => _ItemIndex = value; }

        void RunTheThing()
        {
            //if (Item is not SongItem)
            //    return;

            //SongEditorViewModel vm = new SongEditorViewModel() { song = (SongItem) Item };
            //vm.SongDataUpdated += Vm_SongDataUpdated;
            //SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
            //seq.Show();
        }

    }
}
