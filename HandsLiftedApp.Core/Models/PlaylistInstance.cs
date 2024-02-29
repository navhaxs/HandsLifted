using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Utils;
using Serilog;

namespace HandsLiftedApp.Core.Models
{
    public class PlaylistInstance : Playlist
    {
        public PlaylistInstance()
        {
            _selectedItem = this.WhenAnyValue(
                    (x => x.SelectedItemIndex),
                    (int selectedIndex) =>
                    {
                        if (selectedIndex != -1)
                        {
                            return Items.ElementAtOrDefault(selectedIndex);
                        }

                        return new BlankItem();
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedItem);

            this.WhenAnyValue(x => x.SelectedItem).Subscribe((item) =>
            {
                this.RaisePropertyChanged(nameof(ActiveSlide));
            });

            _selectedItemAsIItemInstance = this.WhenAnyValue(
                    (x => x.SelectedItemIndex),
                    (int selectedIndex) =>
                    {
                        if (selectedIndex != -1)
                        {
                            if (Items.ElementAtOrDefault(selectedIndex) is IItemInstance iItemInstance)
                            {
                                return iItemInstance;
                            }
                        }

                        return new BlankItemInstance(this);
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedItemAsIItemInstance);

            _activeSlide = this.WhenAnyValue(x => x.SelectedItemAsIItemInstance.ActiveSlide, x => x.PresentationState,
                    (Slide activeSlide, PresentationStateEnum presentationState) =>
                    {
                        switch (presentationState)
                        {
                            case PresentationStateEnum.Slides:
                                return activeSlide;
                            case PresentationStateEnum.Logo:
                                return new LogoSlide();
                            case PresentationStateEnum.Blank:
                                return new BlankSlide();
                        }
                        return activeSlide;
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            MessageBus.Current.Listen<UpdateEditedItemMessage>().Subscribe(updateEditedItemMessage =>
            {
                bool found = false;
                int i = -1;
                foreach (var item in Items)
                {
                    i++;
                    if (item.UUID == updateEditedItemMessage.Item.UUID)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    // Items[i] = updateEditedItemMessage.Item;
                    if (Items[i] is SongItemInstance dest)
                    {
                        if (updateEditedItemMessage.Item is SongItemInstance source)
                        {
                            if (dest == source)
                            {
                                return;
                            }

                            dest.Title = source.Title;
                            dest.Arrangement = source.Arrangement;
                            dest.Arrangements = source.Arrangements;
                            dest.SelectedArrangementId = source.SelectedArrangementId;
                            dest.Stanzas = source.Stanzas;
                            dest.Copyright = source.Copyright;
                            dest.Design = source.Design;
                            dest.StartOnTitleSlide = source.StartOnTitleSlide;
                            dest.EndOnBlankSlide = source.EndOnBlankSlide;
                        }
                    }
                }
            });

            MessageBus.Current.Listen<ActionMessage>()
                .Subscribe(x =>
                {
                    switch (x.Action)
                    {
                        case ActionMessage.NavigateSlideAction.NextSlide:
                            NavigateNextSlide();
                            break;
                        case ActionMessage.NavigateSlideAction.PreviousSlide:
                            NavigatePreviousSlide();
                            break;
                    }
                });
            
            this.WhenAnyValue(p => p.LogoGraphicFile)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                .Subscribe(_ =>
            {
                if (LogoGraphicFile == "")
                {
                    return;
                }

                if (AssetLoader.Exists(new Uri(LogoGraphicFile)) || File.Exists(LogoGraphicFile))
                {
                    this.RaisePropertyChanged(nameof(LogoBitmap));
                }

            });
        }

        private DateTime? _lastSaved;

        public DateTime? LastSaved
        {
            get => _lastSaved;
            set => this.RaiseAndSetIfChanged(ref _lastSaved, value);
        }

        private string _playlistFilePath = @"D:\VisionScreensCore-TestData\test.xml";

        public string PlaylistFilePath
        {
            get => _playlistFilePath;
            set => this.RaiseAndSetIfChanged(ref _playlistFilePath, value);
        }

        private string _playlistWorkingDirectory = @"D:\VisionScreensCore-TestData\_Data";

        public string PlaylistWorkingDirectory
        {
            get => _playlistWorkingDirectory;
            set => this.RaiseAndSetIfChanged(ref _playlistWorkingDirectory, value);
        }

        private int _selectedIndex = -1;

        public int SelectedItemIndex
        {
            get => _selectedIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
        }

        private readonly ObservableAsPropertyHelper<Item?> _selectedItem;
        private readonly ObservableAsPropertyHelper<IItemInstance?> _selectedItemAsIItemInstance;

        public Item? SelectedItem
        {
            get => _selectedItem.Value;
        }

        public IItemInstance? SelectedItemAsIItemInstance
        {
            get => _selectedItemAsIItemInstance.Value;
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide.Value;
        }

        public class UpdateEditedItemMessage
        {
            public Item Item { get; set; }
        }

        private PresentationStateEnum _presentationState = PresentationStateEnum.Slides;

        public PresentationStateEnum PresentationState
        {
            get => _presentationState;
            set
            {
                this.RaiseAndSetIfChanged(ref _presentationState, value);
                this.RaisePropertyChanged(nameof(IsLogo));
                this.RaisePropertyChanged(nameof(IsBlank));
            }
        }

        public enum PresentationStateEnum
        {
            Slides,
            Logo,
            Blank
        }

        public bool IsLogo
        {
            get => PresentationState == PresentationStateEnum.Logo;
            set
            {
                this.RaiseAndSetIfChanged(ref _presentationState,
                    value ? PresentationStateEnum.Logo : PresentationStateEnum.Slides, nameof(PresentationState));
            }
        }

        public bool IsBlank
        {
            get => PresentationState == PresentationStateEnum.Blank; set => this.RaiseAndSetIfChanged(ref _presentationState, value ? PresentationStateEnum.Blank : PresentationStateEnum.Slides, nameof(PresentationState));
        }

        public void NavigateNextSlide()
        {
            if (PresentationState != PresentationStateEnum.Slides)
            {
                PresentationState = PresentationStateEnum.Slides;
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
            if (PresentationState != PresentationStateEnum.Slides)
            {
                PresentationState = PresentationStateEnum.Slides;
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
                var item = Items[slideReference.ItemIndex];
                if (item is IItemInstance itemInstance)
                {
                    itemInstance.SelectedSlideIndex = (int)slideReference.SlideIndex;
                }
            }

            SelectedItemIndex = slideReference.ItemIndex;

            if (lastSelectedItemIndex != slideReference.ItemIndex && lastSelectedItemIndex > -1 &&
                Items.ElementAtOrDefault(lastSelectedItemIndex) != null)
            {
                // deselect the slide within the previous item
                var lastItem = Items[lastSelectedItemIndex];
                if (lastItem is IItemInstance itemInstance)
                    itemInstance.SelectedSlideIndex = -1;
            }

            // Log.Debug($"NavigateToReference {slideReference}");

            // MessageBus.Current.SendMessage(new ActiveSlideChangedMessage());
        }

        public SlideReference GetNextSlide(bool allowItemLookAhead = true)
        {
            // if no selected item, attempt to select the first item
            if (SelectedItem == null || SelectedItem is BlankItem)
            {
                int nextNavigatableItemIndex = getNextNavigatableItem(SelectedItemIndex);
                if (nextNavigatableItemIndex != -1)
                {
                    // select first slide of this next item
                    return new SlideReference()
                    {
                        Slide = Items[nextNavigatableItemIndex].GetAsIItemInstance().Slides.ElementAtOrDefault(0),
                        SlideIndex = 0,
                        ItemIndex = nextNavigatableItemIndex
                    };
                }
            }
            // for selected item, attempt to navigate slide forwards (unless at last slide of this item)
            else if (SelectedItemAsIItemInstance != null &&
                     SelectedItem.GetAsIItemInstance().Slides.ElementAtOrDefault(SelectedItemAsIItemInstance.SelectedSlideIndex + 1) != null)
            {
                var nextSlideIndex = SelectedItemAsIItemInstance.SelectedSlideIndex + 1;
                return new SlideReference()
                {
                    Slide = SelectedItem.GetAsIItemInstance().Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }
            // for selected item, if slide group that is loopable, then loop back to first slide of this item
            else if (SelectedItem is SlidesGroupItem &&
                     ((SlidesGroupItem)SelectedItem).IsLooping == true &&
                     SelectedItemAsIItemInstance != null &&
                     (SelectedItem.GetAsIItemInstance().Slides.Count == SelectedItemAsIItemInstance.SelectedSlideIndex + 1))
            {
                var nextSlideIndex = 0;
                return new SlideReference()
                {
                    Slide = SelectedItem.GetAsIItemInstance().Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }
            // else attempt to navigate to next item
            else if (allowItemLookAhead)
            {
                int nextNavigatableItemIndex = getNextNavigatableItem(SelectedItemIndex);

                if (nextNavigatableItemIndex != -1)
                {
                    // select first slide of this next item
                    return new SlideReference()
                    {
                        Slide = Items[nextNavigatableItemIndex].GetAsIItemInstance().Slides.ElementAtOrDefault(0),
                        SlideIndex = 0,
                        ItemIndex = nextNavigatableItemIndex
                    };
                }
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

        private int getNextNavigatableItem(int startIdx)
        {
            for (int idx = startIdx + 1; idx < Items.Count; idx++)
            {
                Item item = Items[idx];
                IItemInstance? itemInstance = item.GetAsIItemInstance();
                if (itemInstance?.Slides.Count > 0)
                {
                    return idx;
                }
            }

            return -1;
        }

        public SlideReference GetPreviousSlide()
        {
            // if no selected item, cannot go futher back, so do nothing 
            if (SelectedItem == null || SelectedItem is BlankItem)
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
            else if (SelectedItemAsIItemInstance != null &&
                     SelectedItem.GetAsIItemInstance().Slides.ElementAtOrDefault(SelectedItemAsIItemInstance.SelectedSlideIndex - 1) != null)
            {
                var nextSlideIndex = SelectedItemAsIItemInstance.SelectedSlideIndex - 1;
                return new SlideReference()
                {
                    Slide = SelectedItem.GetAsIItemInstance().Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }
            // for selected item, if slide group that is loopable, then loop back to last slide of this item
            else if (SelectedItem is SlidesGroupItem &&
                     ((SlidesGroupItem)SelectedItem).IsLooping == true &&
                     SelectedItemAsIItemInstance != null &&
                     (SelectedItemAsIItemInstance.SelectedSlideIndex == 0))
            {
                var nextSlideIndex = SelectedItem.GetAsIItemInstance().Slides.Count - 1;
                return new SlideReference()
                {
                    Slide = SelectedItem.GetAsIItemInstance().Slides[nextSlideIndex],
                    SlideIndex = nextSlideIndex,
                    ItemIndex = SelectedItemIndex
                };
            }

            // else attempt to navigate item backwards to last slide of previous item
            int previousNavigatableItemIndex = getPreviousNavigatableItem(SelectedItemIndex);
            if (previousNavigatableItemIndex != -1)
            {
                var nextSlideIndex = Items[previousNavigatableItemIndex].GetAsIItemInstance().Slides.Count - 1;

                return new SlideReference()
                {
                    Slide = Items[previousNavigatableItemIndex].GetAsIItemInstance().Slides.ElementAtOrDefault(nextSlideIndex),
                    SlideIndex = nextSlideIndex,
                    ItemIndex = previousNavigatableItemIndex
                };
            }

            // else no item to navigate backwards to, so deselect current item and its slide
            // i.e. puts the cursor before the first item
            return new SlideReference()
            {
                Slide = null,
                SlideIndex = null,
                ItemIndex = -1
            };
        }

        private int getPreviousNavigatableItem(int startIdx)
        {
            for (int idx = startIdx - 1; idx >= 0; idx--)
            {
                Item item = Items[idx];
                IItemInstance? itemInstance = item.GetAsIItemInstance();
                if (itemInstance?.Slides.Count > 0)
                {
                    return idx;
                }
            }

            return -1;
        }

        #region CachedBitmaps

        // TODO this could be a ObservableAsPropertyHelper
        public Bitmap? LogoBitmap
        {
            get
            {
                if (Design.IsDesignMode)
                {
                    return null;
                }
                try
                {
                    return BitmapLoader.LoadBitmap(LogoGraphicFile);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to load Bitmap for LogoBitmap {LogoGraphicFile}", (LogoGraphicFile));
                    return null;
                }
            }
        }

        #endregion
    }
}