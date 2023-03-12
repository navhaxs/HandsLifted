using Avalonia.Animation;
using Avalonia.Threading;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views;
using HandsLiftedApp.Views.Editor;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models.ItemState
{
    // Move into PlaylistState
    public class ItemStateImpl : ViewModelBase, IItemState
    {
        public Item<ItemStateImpl> parent { get; set; }

        //private SlideStateBase selectedItem;

        public ItemStateImpl(ref Item<ItemStateImpl> item)
        {
            parent = item;
            EditCommand = ReactiveCommand.Create(RunTheThing);

            _selectedSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, (selectedIndex) =>
                {
                    if (selectedIndex > -1 && parent.Slides != null && parent.Slides.Count > selectedIndex)
                        return parent.Slides[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedSlide);

            // or really, "has selected?"
            _isSelected = this.WhenAnyValue(x => x.SelectedSlideIndex, (selectedIndex) => selectedIndex != null && selectedIndex != -1)
                .ToProperty(this, x => x.IsSelected);

            Dispatcher.UIThread.InvokeAsync(() => {
                PageTransition = new XFade(TimeSpan.FromSeconds(0.500));
            });

            Observable.CombineLatest(
                this.WhenAnyValue(o => o.SelectedSlideIndex),
                this.WhenAnyValue(o => o.parent.Slides),
                (a, b) =>
                {

                    return Unit.Default;
                }
            );



            //this.WhenAnyValue(x => x.parent.Slides)
            //    .Subscribe(x => { 

            //        foreach (var c in x)
            //        {
            //            c.Sele
            //        }
            //    });

        }

        //public int Index { get; set; }

        //private Item<ItemStateImpl> _item;
        //public Item<ItemStateImpl> Item { get => _item; set => this.RaiseAndSetIfChanged(ref _item, value); }
        private bool _manualSelectionIndex = false;

        public bool LockSelectionIndex
        {
            get => _manualSelectionIndex; set
            {
                this.RaiseAndSetIfChanged(ref _manualSelectionIndex, value);
            }
        }

        private int _selectedIndex = -1;

        public int SelectedSlideIndex
        {
            get => _selectedIndex; set
            {
                if (LockSelectionIndex)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _selectedIndex, value);

                
                // !!! DO NOT CALL THIS IF NOT ACTIVE ITEM
                // if (_selectedIndex > -1)
                // {

                    // MessageBus.Current.SendMessage(new ActiveSlideChangedMessage() { SourceItem = parent });
                // }
            }
        }

        private ObservableAsPropertyHelper<bool> _isSelected;
        public bool IsSelected { get => _isSelected.Value; }

        private ObservableAsPropertyHelper<Slide?> _selectedSlide;
        public Slide? SelectedSlide { get => _selectedSlide.Value; }
        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        // private int _ItemIndex;
        // public int ItemIndex { get => _ItemIndex; set => _ItemIndex = value; }

        private IPageTransition? _pageTransition;
        public IPageTransition? PageTransition { get => _pageTransition; set => this.RaiseAndSetIfChanged(ref _pageTransition, value); }

        private string? _test;
        public string? Test { get => _test; set => this.RaiseAndSetIfChanged(ref _test, value); }

        void RunTheThing()
        {
            if (parent is SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)
            {
                SongEditorViewModel vm = new SongEditorViewModel() { song = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)parent };
                //vm.SongDataUpdated += Vm_SongDataUpdated;
                SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                seq.Show();
            }

            if (parent is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)
            {
                //SongEditorViewModel vm = new SongEditorViewModel() { song = (SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)parent };
                //vm.SongDataUpdated += Vm_SongDataUpdated;

                GroupItemsEditorWindow seq = new GroupItemsEditorWindow() { DataContext = ((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)parent) };
                seq.Show();
            }
        }

    }
}
