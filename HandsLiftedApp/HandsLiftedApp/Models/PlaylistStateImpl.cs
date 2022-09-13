using Avalonia.Controls;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Models
{
    public class PlaylistStateImpl : ViewModelBase, IPlaylistState
    {
        Playlist<PlaylistStateImpl, ItemStateImpl> Playlist;
        public PlaylistStateImpl(ref Playlist<PlaylistStateImpl, ItemStateImpl> playlist)
        {
            Playlist = playlist;

            MessageBus.Current.Listen<ActiveSlideChangedMessage>()
                .Subscribe(x =>
                {
                    var lastSelectedIndex = Playlist.State.SelectedItemIndex;

                    var sourceIndex = Playlist.Items.IndexOf(x.SourceItem);


                    // update the selected item in the playlist
                    Playlist.State.SelectedItemIndex = sourceIndex;


                    // notify the last deselected item in the playlist
                    if (lastSelectedIndex > -1 && sourceIndex != lastSelectedIndex && Playlist != null)
                    {
                        Playlist.Items[lastSelectedIndex].State.SelectedSlideIndex = -1;
                    }
                });

            _selectedItem = this.WhenAnyValue(x => x.SelectedItemIndex, (selectedIndex) =>
                {
                    if (selectedIndex != -1)
                        return Playlist.Items[selectedIndex];

                    return null;
                })
                .ToProperty(this, x => x.SelectedItem);

            _activeSlide = this.WhenAnyValue(x => x.SelectedItem.State.SelectedSlide,
                    (Slide selectedSlide) => selectedSlide)
                    .ToProperty(this, c => c.ActiveSlide);

            _nextSlide = this.WhenAnyValue((PlaylistStateImpl x) => x.SelectedItem.State.SelectedSlide,
                    (Slide selectedSlide) => GetNextSlide().Slide)
                    .ToProperty(this, c => c.NextSlide);
            
            _nextSlideWithinItem = this.WhenAnyValue((PlaylistStateImpl x) => x.SelectedItem.State.SelectedSlide,
                    (Slide selectedSlide) => GetNextSlide(false).Slide)
                    .ToProperty(this, c => c.NextSlideWithinItem);

            _previousSlide = this.WhenAnyValue(x => x.SelectedItem.State.SelectedSlide,
                    (Slide selectedSlide) => GetPreviousSlide().Slide)
                    .ToProperty(this, c => c.PreviousSlide);

            if (Design.IsDesignMode)
            {
                SelectedItemIndex = 0;
            }
        }

        public SlideReference GetNextSlide(bool allowItemLookAhead = true)
        {
            // if no selected item, attempt to select the first item
            if (SelectedItem == null)
            {
                if (Playlist.Items.Count > 0)
                {
                    return new SlideReference()
                    {
                        Slide = Playlist.Items[0].Slides[0],
                        SlideIndex = 0,
                        ItemIndex = 0,
                    };
                }
            }
            // for selected item, attempt to navigate slide forwards
            // unless at last slide
            else if (SelectedItem.Slides.ElementAtOrDefault(SelectedItem.State.SelectedSlideIndex + 1) != null)
            {
                var nextSlideIndex = SelectedItem.State.SelectedSlideIndex + 1;
                return new SlideReference()
                {
                    Slide = SelectedItem.Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }
            // else attempt to navigate item forwards
            else if (allowItemLookAhead && Playlist.Items.ElementAtOrDefault(SelectedItemIndex + 1) != null)
            {
                var nextItemIndex = SelectedItemIndex + 1;
                // select first slide of this next item
                return new SlideReference()
                {
                    Slide = Playlist.Items[nextItemIndex].Slides.ElementAtOrDefault(0),
                    SlideIndex = 0,
                    ItemIndex = nextItemIndex
                };

            }
            return new SlideReference()
            {
                Slide = null,
                SlideIndex = null,
                ItemIndex = -1
            };
        }

        public SlideReference GetPreviousSlide()
        {
            // if no selected item, cannot go futher back, so do nothing 
            if (SelectedItem == null)
            {
                return new SlideReference()
                {
                    Slide = null,
                    SlideIndex = null,
                    ItemIndex = -1
                };
            }
            // for selected item, attempt to navigate slide backwards within the item
            // (unless at first slide)
            else if (SelectedItem.Slides.ElementAtOrDefault(SelectedItem.State.SelectedSlideIndex - 1) != null)
            {
                var nextSlideIndex = SelectedItem.State.SelectedSlideIndex - 1;
                return new SlideReference()
                {
                    Slide = SelectedItem.Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex 
                };
            }
            // else attempt to navigate item backwards to last slide of previous item
            else if (Playlist.Items.ElementAtOrDefault(SelectedItemIndex - 1) != null)
            {
                var nextItemIndex = SelectedItemIndex - 1;
                var nextSlideIndex = Playlist.Items[nextItemIndex].Slides.Count - 1;

                return new SlideReference()
                {
                    Slide = Playlist.Items[nextItemIndex].Slides.ElementAtOrDefault(nextSlideIndex),
                    SlideIndex = nextSlideIndex,
                    ItemIndex = nextItemIndex
                };
            }
            // else no item to navigate backwards to, so deselect current item and its slide
            // i.e. puts the cursor before the first item
            else
            {
                return new SlideReference()
                {
                    Slide = null,
                    SlideIndex = null,
                    ItemIndex = -1
                };
            }
        }

        public string _playlistWorkingDirectory = @"C:\VisionScreens\TestPlaylist";
        public string PlaylistWorkingDirectory { get => _playlistWorkingDirectory; set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value); }

        private int selectedIndex = -1;
        public int SelectedItemIndex
        {
            get { return selectedIndex; }
            set
            {
                this.RaiseAndSetIfChanged(ref selectedIndex, value);
            }
        }

        private ObservableAsPropertyHelper<Item<ItemStateImpl>?> _selectedItem;
        public Item<ItemStateImpl>? SelectedItem { get => _selectedItem.Value; }

        private ObservableAsPropertyHelper<Slide> _activeSlide;
        public Slide ActiveSlide { get => _activeSlide.Value; }

        private ObservableAsPropertyHelper<Slide?> _nextSlide;
        public Slide? NextSlide { get => _nextSlide.Value; }

        private ObservableAsPropertyHelper<Slide?> _nextSlideWithinItem;
        public Slide? NextSlideWithinItem { get => _nextSlideWithinItem.Value; }

        private ObservableAsPropertyHelper<Slide?> _previousSlide;
        public Slide? PreviousSlide { get => _previousSlide.Value; }

        // TODO: "Presentation State" can be moved out of playlist state.
        private bool _isLogo = false;
        public bool IsLogo { get => _isLogo; set => this.RaiseAndSetIfChanged(ref _isLogo, value); }

        private bool _isBlank = false;
        public bool IsBlank { get => _isBlank; set => this.RaiseAndSetIfChanged(ref _isBlank, value); }

        private bool _isFreeze = false;
        public bool IsFreeze { get => _isFreeze; set => this.RaiseAndSetIfChanged(ref _isFreeze, value); }

        public void NavigateNextSlide()
        {
            SlideReference slideReference = GetNextSlide();
            NavigateToReference(slideReference);
        }
        public void NavigatePreviousSlide()
        {
            SlideReference slideReference = GetPreviousSlide();
            NavigateToReference(slideReference);
        }

        public void NavigateToReference(SlideReference slideReference)
        {
            var lastSelectedItemIndex = SelectedItemIndex;

            SelectedItemIndex = slideReference.ItemIndex;

            if (slideReference.SlideIndex != null)
            {
                Playlist.Items[slideReference.ItemIndex].State.SelectedSlideIndex = (int)slideReference.SlideIndex;
            }

            if (lastSelectedItemIndex != slideReference.ItemIndex && lastSelectedItemIndex > -1)
            {
                // deselect the slide within the previous item
                Playlist.Items[lastSelectedItemIndex].State.SelectedSlideIndex = -1;
            }
        }
    }
}
