using Avalonia.Animation;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views.Editor;
using HandsLiftedApp.XTransitioningContentControl;
using ReactiveUI;
using System;
using System.Reactive;

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

            _selectedSlide = this.WhenAnyValue(x => x.SelectedIndex, x => x.LockSelectionIndex, (selectedIndex, lockSelectionIndex) =>
                {
                    if (selectedIndex > -1 && parent.Slides != null && parent.Slides.Count > selectedIndex)
                        return parent.Slides[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedSlide);

            // or really, "has selected?"
            _isSelected = this.WhenAnyValue(x => x.SelectedIndex, (selectedIndex) => selectedIndex != null && selectedIndex != -1)
                .ToProperty(this, x => x.IsSelected);

            PageTransition = new XFade(TimeSpan.FromSeconds(0.300));
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

        public int SelectedIndex
        {
            get => _selectedIndex; set
            {
                if (LockSelectionIndex)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _selectedIndex, value);

                if (_selectedIndex > -1)
                {

                    MessageBus.Current.SendMessage(new ActiveSlideChangedMessage() { SourceItem = parent });
                }
            }
        }

        private ObservableAsPropertyHelper<bool> _isSelected;
        public bool IsSelected { get => _isSelected.Value; }

        private ObservableAsPropertyHelper<Slide> _selectedSlide;
        public Slide SelectedSlide { get => _selectedSlide.Value; }

        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        private int _ItemIndex;
        public int ItemIndex { get => _ItemIndex; set => _ItemIndex = value; }

        private IPageTransition? _pageTransition;
        public IPageTransition? PageTransition { get => _pageTransition; set => this.RaiseAndSetIfChanged(ref _pageTransition, value); }

        private string? _test;
        public string? Test { get => _test; set => this.RaiseAndSetIfChanged(ref _test, value); }

        void RunTheThing()
        {
            if (parent is not SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)
                return;

            SongEditorViewModel vm = new SongEditorViewModel() { song = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)parent };
            //vm.SongDataUpdated += Vm_SongDataUpdated;
            SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
            seq.Show();
        }

    }
}
