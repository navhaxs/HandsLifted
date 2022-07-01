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
    public class ItemState : ViewModelBase
    {
        private int _selectedIndex;
        private SlideStateBase selectedItem;

        public ItemState()
        {
            EditCommand = ReactiveCommand.Create(RunTheThing);

            _SlideStates = this.WhenAnyValue(x => x.Item,
                (item) =>
                {
                    if (item == null)
                        return new ObservableCollection<SlideStateBase>();

                    return new ObservableCollection<SlideStateBase>(item.Slides.Select((s, index) => convertDataToState(s, index)).ToList());
                })
                .ToProperty(this, x => x.SlideStates);

            UpdateItemStates();
        }

        public int Index { get; set; }

        private Item? _item;
        public Item? Item { get => _item; set => this.RaiseAndSetIfChanged(ref _item, value); }

        private ObservableAsPropertyHelper<ObservableCollection<SlideStateBase>> _SlideStates;
        public ObservableCollection<SlideStateBase> SlideStates { get => _SlideStates.Value; }

        // slide index here?
        public int SelectedIndex
        {
            get => _selectedIndex; set
            {
                this.RaiseAndSetIfChanged(ref _selectedIndex, value);

                if (_selectedIndex > -1)
                {
                    MessageBus.Current.SendMessage(new ActiveSlideChangedMessage() { SourceItemStateIndex = Index });
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

        public ReactiveCommand<Unit, Unit> EditCommand { get; }


        void RunTheThing()
        {
            SongEditorViewModel vm = new SongEditorViewModel() { song = (SongItem) Item };
            vm.SongDataUpdated += Vm_SongDataUpdated;
            SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
            seq.Show();
        }

        void UpdateItemStates()
        {
            if (Item == null)
                return;

            List<SlideStateBase> slideStateBases = Item.Slides.Select((s, index) => convertDataToState(s, index)).ToList(); ;
            var x = new ObservableCollection<SlideStateBase>(slideStateBases);
            SlideStates.UpdateCollection(x);
        }
        private void Vm_SongDataUpdated(object? sender, EventArgs e)
        {
            Item = ((ThresholdReachedEventArgs)e).UpdatedSongData;
            UpdateItemStates();
        }
    }
}
