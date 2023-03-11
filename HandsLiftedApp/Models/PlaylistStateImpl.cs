using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DynamicData.Binding;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
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

            _selectedItem = Observable.CombineLatest(
            this.WhenAnyValue(x => x.SelectedItemIndex),
                Playlist.Items.ObserveCollectionChanges(),
                (selectedIndex, items) => {
                    if (selectedIndex != -1)
                    {
                        return Playlist.Items.ElementAtOrDefault(selectedIndex);
                    }

                    return new BlankItem<ItemStateImpl>();
                }
            ).ToProperty(this, x => x.SelectedItem);

            // note re: null https://www.reactiveui.net/docs/handbook/when-any/#watching-a-nested-property
            // "If either Foo or Bar are null then the value of Baz won't be emitted."
            // so we use BlankItem as a workaround.
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

            Playlist.Items.CollectionChanged += Items_CollectionChanged;

            this.WhenAnyValue(x => x.SelectedItem).Subscribe((Item<ItemStateImpl>? c) =>
            {
                if (c == null)
                {
                    Debug.Print("null");
                    return;
                }

                
                Debug.Print(c.ToString());
            });

            Playlist.WhenAnyValue(p => p.LogoGraphicFile).Subscribe(_ =>
            {

                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                if (assets.Exists(new Uri(Playlist.LogoGraphicFile)))
                {
                    this.RaisePropertyChanged(nameof(LogoBitmap));
                    this.RaisePropertyChanged("LogoBitmap");
                }

            });

        }

        public Bitmap LogoBitmap
        {
            get
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                return new Bitmap(assets.Open(new Uri(Playlist.LogoGraphicFile)));
            }
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action ==System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                //if ()
                //e.OldItems
                //SelectedItemIndex = -1;
            }
        }

        public SlideReference GetNextSlide(bool allowItemLookAhead = true)
        {
            // if no selected item, attempt to select the first item
            if (SelectedItem == null || SelectedItem is BlankItem<ItemStateImpl>)
            {
                if (Playlist.Items.Count > 0)
                {
                    return new SlideReference()
                    {
                        Slide = Playlist.Items[0]?.Slides.FirstOrDefault(),
                        SlideIndex = 0,
                        ItemIndex = 0,
                    };
                }
            }
            // for selected item, attempt to navigate slide forwards (unless at last slide of this item)
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
            // for selected item, if slide group that is loopable, then loop back to first slide of this item
            else if (SelectedItem is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> &&
                ((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)SelectedItem).IsLooping == true &&
                (SelectedItem.Slides.Count == SelectedItem.State.SelectedSlideIndex + 1))
            {
                var nextSlideIndex = 0;
                return new SlideReference()
                {
                    Slide = SelectedItem.Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }
            // else attempt to navigate to next item
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
            // no more next slide.
            // NOTE: do not want to navigate to "unselected" as this actually goes back to initial slide before all items!!
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
            if (SelectedItem == null || SelectedItem is BlankItem<ItemStateImpl>)
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
            // for selected item, if slide group that is loopable, then loop back to last slide of this item
            else if (SelectedItem is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> &&
                ((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)SelectedItem).IsLooping == true &&
                (SelectedItem.State.SelectedSlideIndex == 0))
            {
                var nextSlideIndex = SelectedItem.Slides.Count - 1;
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
            if (IsLogo)
            {
                IsLogo = false;
                return;
            }
            if (IsBlank)
            {
                IsBlank = false;
                return;
            }

            SlideReference slideReference = GetNextSlide();

            // the next slide is NEVER itemIndex > -1
            // that -1 is however used for StageDisplay and Previews
            if (slideReference.ItemIndex > -1)
            {
                NavigateToReference(slideReference);
            }
        }
        public void NavigatePreviousSlide()
        {
            if (IsLogo)
            {
                IsLogo = false;
                return;
            }
            if (IsBlank)
            {
                IsBlank = false;
                return;
            }

            SlideReference slideReference = GetPreviousSlide();
            NavigateToReference(slideReference);
        }

        public void NavigateToReference(SlideReference slideReference)
        {
            var lastSelectedItemIndex = SelectedItemIndex;

            if (slideReference.SlideIndex != null)
            {
                Playlist.Items[slideReference.ItemIndex].State.SelectedSlideIndex = (int)slideReference.SlideIndex;
            }

            SelectedItemIndex = slideReference.ItemIndex;

            if (lastSelectedItemIndex != slideReference.ItemIndex && lastSelectedItemIndex > -1 && Playlist.Items.ElementAtOrDefault(lastSelectedItemIndex) != null)
            {
                // deselect the slide within the previous item
                Playlist.Items[lastSelectedItemIndex].State.SelectedSlideIndex = -1;
            }
            
            MessageBus.Current.SendMessage(new ActiveSlideChangedMessage());
        }
    }
}
