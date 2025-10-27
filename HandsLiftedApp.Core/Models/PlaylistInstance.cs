using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Models
{
    public class PlaylistInstance : Playlist, IDisposable
    {
        private List<IDisposable> _disposables = new();

        public PlaylistInstance()
        {
            _selectedItem = this.WhenAnyValue(
                    (x => x.SelectedItemIndex),
                    (int selectedIndex) =>
                    {
                        if (selectedIndex != -1)
                        {
                            var result = Items.ElementAtOrDefault(selectedIndex);
                            if (result != null)
                            {
                                return result;
                            }
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
                            var result = Items.ElementAtOrDefault(selectedIndex);
                            if (result is IItemInstance iItemInstance)
                            {
                                return iItemInstance;
                            }
                        }

                        return new BlankItemInstance(this);
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedItemAsIItemInstance);

            _activeSlide = this.WhenAnyValue(x => x.SelectedItemAsIItemInstance.ActiveSlide, x => x.PresentationState, x => x.QuickShowItem,
                    (Slide activeSlide, PresentationStateEnum presentationState, Slide? quickShowSlide) =>
                    {
                        switch (presentationState)
                        {
                            case PresentationStateEnum.Slides:
                                return activeSlide;
                            case PresentationStateEnum.Logo:
                                return new LogoSlide();
                            case PresentationStateEnum.Blank:
                                return new BlankSlide();
                            case PresentationStateEnum.QuickShow:
                                return quickShowSlide ?? new BlankSlide();
                        }

                        return activeSlide;
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            // TODO: what if the next item changes, without the active slide changing this will never get retriggered...!!
            _nextSlide = this.WhenAnyValue(x => x.ActiveSlide,
                    (Slide activeSlide) =>
                    {
                        SlideReference slideReference = GetNextSlide();
                        return slideReference.Slide;
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.NextSlide);
            
            if (Design.IsDesignMode)
            {
                return;
            }

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
                        case ActionMessage.NavigateSlideAction.GotoLogo:
                            IsLogo = true;
                            break;
                        case ActionMessage.NavigateSlideAction.GotoBlank:
                            IsBlank = true;
                            break;
                    }
                });

            this.WhenAnyValue(p => p.LogoGraphicFile)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                .Subscribe(_ =>
                {
                    if (LogoGraphicFile == "" || LogoGraphicFile == null)
                    {
                        this.RaisePropertyChanged(nameof(LogoBitmap));
                        return;
                    }

                    try
                    {
                        if (File.Exists(LogoGraphicFile) || AssetLoader.Exists(new Uri(LogoGraphicFile)))
                        {
                            this.RaisePropertyChanged(nameof(LogoBitmap));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("LogoGraphicFile read failed", ex);
                    }
                });

            // try load Playlist Logo > Global Logo
            _disposables.Add(
            MessageBus.Current.Listen<AddItemByFilePathMessage>()
                .Subscribe(addItemToPlaylistMessage =>
                {
                    int insertAt = addItemToPlaylistMessage.insertIndex ?? Items.Count;
                    var itemsToInsert = addItemToPlaylistMessage.filePaths;
                    itemsToInsert.Reverse();
                    // todo: smart logic
                    // for multiple files, if all are media type, combine into single new group
                    // else process each file separately
                    // and insert at INDEX
                    foreach (var filePath in itemsToInsert)
                    {
                        var newItem = CreateItem.GenerateItem(filePath);
                        if (newItem != null)
                        {
                            var newItemInstance = ItemInstanceFactory.ToItemInstance(newItem, this);
                            if (insertAt >= 0)
                            {
                                Items.Insert(insertAt, newItemInstance);
                            }
                            else
                            {
                                Items.Add(newItemInstance);
                            }
                            
                            if (newItemInstance is IItemSyncable instance)
                            {
                                instance.Sync();
                            }
                        }
                    }
                    
                    MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = insertAt });

                }));

            _logoBitmap = this.WhenAnyValue(p => p.LogoGraphicFile)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                .Select(x =>
                {
                    if (string.IsNullOrEmpty(x))
                    {
                        return null;
                    }

                    try
                    {
                        if (File.Exists(x) || AssetLoader.Exists(new Uri(x)))
                        {
                            return BitmapLoader.LoadBitmap(x);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("LogoGraphicFile read failed", ex);
                    }

                    return null;
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.LogoBitmap);

            _stageDisplaySlideCountText = this.WhenAnyValue(s => s.SelectedItemAsIItemInstance.SelectedSlideIndex,
                    s => s.SelectedItemAsIItemInstance.Slides.Count, ((i, i1) => $"Slide {i + 1}/{i1}"))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.StageDisplaySlideCountText);

            _disposables.Add(MessageBus.Current.Listen<NavigateToSlideReferenceAction>()
                    .Subscribe(action => NavigateToReference(action.SlideReference)));

            this.WhenAnyValue(p => p.Items)
                .Subscribe(items =>
                {
                    items.CollectionChanged -= OnItemsCollectionChanged;
                    items.CollectionChanged += OnItemsCollectionChanged;
                });

            this.WhenAnyValue(
                    p => p.Title,
                    p => p.LogoGraphicFile,
                    p => p.Designs,
                    p => p.Items)
                .Subscribe(_ => IsDirty = true);
        }

        public void UpdateIndexes()
        {
            foreach (var (item, index) in Items.WithIndex())
            {
                item.Index = index;
            }
            SelectedItemIndex = SelectedItem == null ? -1 : Items.IndexOf(SelectedItem);
        }

        public void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateIndexes();
            IsDirty = true;
        }

        private PlaylistItemInstanceCollection<Item> _items = new();

        public new PlaylistItemInstanceCollection<Item> Items
        {
            get => _items;
            set
            {
                value.ItemDataModified -= OnValueOnItemDataModified;
                this.RaiseAndSetIfChanged(ref _items, value);
                UpdateIndexes();
                value.ItemDataModified += OnValueOnItemDataModified;
            }
        }

        private void OnValueOnItemDataModified(object? sender, EventArgs args)
        {
            IsDirty = true;
        }

        public AutoAdvanceTimerController AutoAdvanceTimer { get; init; } = new();

        private DateTime? _lastSaved;

        public DateTime? LastSaved
        {
            get => _lastSaved;
            set => this.RaiseAndSetIfChanged(ref _lastSaved, value);
        }

        private string? _playlistFilePath = null;

        public string? PlaylistFilePath
        {
            get => _playlistFilePath;
            set => this.RaiseAndSetIfChanged(ref _playlistFilePath, value);
        }

        private int? _activeItemInsertIndex = null;

        /// <summary>
        /// Index of where to insert the item of the active Add Button window
        /// </summary>
        public int? ActiveItemInsertIndex
        {
            get => _activeItemInsertIndex;
            set => this.RaiseAndSetIfChanged(ref _activeItemInsertIndex, value);
        }

        private bool _isDirty = true;

        /// <summary>
        /// File dirty bit = document has pending updates that are unsaved to disk
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        private string _playlistWorkingDirectory = @"VisionScreensUserData\";

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

        private Slide? _quickShowItem;
        public Slide? QuickShowItem
        {
            
            get => _quickShowItem;
            set => this.RaiseAndSetIfChanged(ref _quickShowItem, value);
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide.Value;
        }

        private ObservableAsPropertyHelper<Slide> _nextSlide;

        public Slide NextSlide
        {
            get => _nextSlide.Value;
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
                this.RaisePropertyChanged(nameof(IsQuickShow));
            }
        }

        public enum PresentationStateEnum
        {
            Slides,
            Logo,
            Blank,
            QuickShow
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
            get => PresentationState == PresentationStateEnum.Blank;
            set => this.RaiseAndSetIfChanged(ref _presentationState,
                value ? PresentationStateEnum.Blank : PresentationStateEnum.Slides, nameof(PresentationState));
        }

        public bool IsQuickShow
        {
            get => PresentationState == PresentationStateEnum.QuickShow;
            set => this.RaiseAndSetIfChanged(ref _presentationState,
                value ? PresentationStateEnum.QuickShow : PresentationStateEnum.Slides, nameof(PresentationState));
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
            if (slideReference is { ItemUUID: not null, ItemIndex: null })
            {
                var lookup = Items.FirstOrDefault(x => x.UUID == slideReference.ItemUUID)?.Index ?? null;
                if (lookup != null)
                {
                    slideReference.ItemIndex = lookup;
                }
            }
            
            var nextItemIndex = slideReference.ItemIndex ?? SelectedItemIndex;// TODO SelectedItemIndex is -1

            if (nextItemIndex < 0 || nextItemIndex >= Items.Count)
            {
                Log.Error("NavigateToReference {SlideReference} out of bounds", slideReference);
                return;
            }

            var lastSelectedItemIndex = SelectedItemIndex; // TODO this can be wrong

            IItemInstance? currentItemInstance = null;
            if (slideReference.SlideIndex != null)
            {
                var baseItemInstance = Items[nextItemIndex];
                if (baseItemInstance is IItemInstance itemInstance)
                {
                    currentItemInstance = itemInstance;
                    itemInstance.SelectedSlideIndex = (int)slideReference.SlideIndex;
                }
            }

            SelectedItemIndex = nextItemIndex;

            if (lastSelectedItemIndex != nextItemIndex && lastSelectedItemIndex > -1 &&
                Items.ElementAtOrDefault(lastSelectedItemIndex) != null)
            {
                // deselect the slide within the previous item
                var lastBaseItemInstance = Items[lastSelectedItemIndex];
                if (lastBaseItemInstance is IItemInstance itemInstance)
                    itemInstance.SelectedSlideIndex = -1;
            }

            // lastly clear LOGO
            if (PresentationState != PresentationStateEnum.Slides)
            {
                PresentationState = PresentationStateEnum.Slides;
            }

            Log.Debug("NavigateToReference {SlideReference}", slideReference);

            // MessageBus.Current.SendMessage(new ActiveSlideChangedMessage());

            AutoAdvanceTimer.OnSlideNavigation(currentItemInstance);
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
                     SelectedItem.GetAsIItemInstance().Slides
                         .ElementAtOrDefault(SelectedItemAsIItemInstance.SelectedSlideIndex + 1) != null)
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
                     (SelectedItem.GetAsIItemInstance().Slides.Count ==
                      SelectedItemAsIItemInstance.SelectedSlideIndex + 1))
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
                Item? item = Items[idx];
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
            // if no selected item, cannot go further back, so do nothing 
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
                     SelectedItem.GetAsIItemInstance().Slides
                         .ElementAtOrDefault(SelectedItemAsIItemInstance.SelectedSlideIndex - 1) != null)
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
                    Slide = Items[previousNavigatableItemIndex].GetAsIItemInstance().Slides
                        .ElementAtOrDefault(nextSlideIndex),
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

        private ObservableAsPropertyHelper<Bitmap?> _logoBitmap;
        public Bitmap? LogoBitmap => _logoBitmap.Value;

        #endregion

        private readonly ObservableAsPropertyHelper<string?> _stageDisplaySlideCountText;

        public string? StageDisplaySlideCountText
        {
            get => _stageDisplaySlideCountText.Value;
        }

        public void Dispose()
        {
            AutoAdvanceTimer.Dispose();
            _selectedItem.Dispose();
            _selectedItemAsIItemInstance.Dispose();
            _activeSlide.Dispose();
            _nextSlide.Dispose();
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}