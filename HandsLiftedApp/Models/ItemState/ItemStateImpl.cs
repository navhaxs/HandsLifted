using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views;
using HandsLiftedApp.Views.App;
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

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                PageTransition = new XFade(TimeSpan.FromSeconds(0.20));
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

        private IPageTransition? _pageTransition;
        public IPageTransition? PageTransition { get => _pageTransition; set => this.RaiseAndSetIfChanged(ref _pageTransition, value); }

        private int _itemIndex;
        public int ItemIndex { get => _itemIndex; set => this.RaiseAndSetIfChanged(ref _itemIndex, value); }

        void RunTheThing()
        {
            // todo datatemplates

            Window itemEditorWindow = null;

            if (parent is LogoItem<ItemStateImpl> || parent is SectionHeadingItem<ItemStateImpl>)
            {
                itemEditorWindow = new LogoEditorWindow() { DataContext = parent };
            }

            if (parent is SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)
            {
                SongEditorViewModel vm = new SongEditorViewModel() { song = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)parent };
                itemEditorWindow = new SongEditorWindow() { DataContext = vm };
            }

            if (parent is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)
            {
                GroupItemsEditorViewModel vm = new GroupItemsEditorViewModel()
                {
                    Item = ((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)parent)
                };

                itemEditorWindow = new GroupItemsEditorWindow() { DataContext = vm };
            }

            if (itemEditorWindow != null)
            {
                MessageBus.Current.SendMessage(new MainWindowModalMessage(itemEditorWindow, false));
            }

        }

    }
}
