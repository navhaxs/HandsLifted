using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class MediaGroupItemInstance : MediaGroupItem, IItemInstance, IItemDirtyBit {
        public PlaylistInstance ParentPlaylist { get; set; }
        
        public event EventHandler ItemDataModified;

        private BlankSlide _blankSlide = new();

        public MediaGroupItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
            // SelectedSlideIndex is an index into Slides. Callers that reorder slides (see
            // MainViewModel's MoveSlideCommand handler) must recalculate it by locating the
            // previously-selected Slide instance in the post-reorder Slides collection.
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides, (selectedSlideIndex, slides) =>
                {
                    try
                    {
                        if (selectedSlideIndex > -1 && selectedSlideIndex < slides.Count)
                        {
                            return slides.ElementAt(selectedSlideIndex);
                        }
                    }
                    catch (System.Exception _ignored) { }

                    return _blankSlide;
                })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Items, // TODO not working
                i => i.Title,
                i => i.AutoAdvanceTimer
            ).Subscribe(_ =>
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            });

            // TODO not working
            // Items.CollectionItemChanged += (sender, args) => IsDirty = true;
            // Items.CollectionChanged += (sender, args) => IsDirty = true;
        }

        // Cache of GroupItem -> generated Slide, so that reordering Items (or regenerating
        // slides for an unrelated reason) reuses the same Slide instance instead of
        // constructing a fresh one (CreateItem.GenerateMediaContentSlide always `new`s a
        // fresh Slide for a MediaItem). Preserving identity lets callers track the
        // currently-selected Slide by reference across a GenerateSlides() call.
        private readonly Dictionary<MediaGroupItem.MediaItem, (string? Path, Slide Slide)> _mediaSlideCache = new();

        private Slide? GetOrCreateSlide(MediaGroupItem.GroupItem item)
        {
            if (item is MediaGroupItem.MediaItem mediaItem)
            {
                if (_mediaSlideCache.TryGetValue(mediaItem, out var cached) &&
                    cached.Path == mediaItem.SourceMediaFilePath)
                {
                    return cached.Slide;
                }

                var slide = CreateItem.GenerateMediaContentSlide(item, this);
                if (slide != null)
                {
                    _mediaSlideCache[mediaItem] = (mediaItem.SourceMediaFilePath, slide);
                }
                else
                {
                    _mediaSlideCache.Remove(mediaItem);
                }

                return slide;
            }

            // SlideItem.SlideData (and any other future GroupItem types) already has stable
            // identity across calls - CreateItem.GenerateMediaContentSlide returns it directly.
            return CreateItem.GenerateMediaContentSlide(item, this);
        }

        public void GenerateSlides()
        {
            var newOrder = new List<Slide>();
            foreach (var item in Items)
            {
                var slide = GetOrCreateSlide(item);
                if (slide != null)
                {
                    newOrder.Add(slide);
                }
            }

            if (_mediaSlideCache.Count > 0)
            {
                var currentMediaItems = new HashSet<MediaGroupItem.MediaItem>(Items.OfType<MediaGroupItem.MediaItem>());
                foreach (var staleKey in _mediaSlideCache.Keys.Where(k => !currentMediaItems.Contains(k)).ToList())
                {
                    _mediaSlideCache.Remove(staleKey);
                }
            }

            // Sync _slides in place (Move/Insert/Remove) rather than replacing the collection,
            // so bound controls (e.g. the slide thumbnail strip) update incrementally instead
            // of tearing down and recreating every container on every regeneration/reorder.
            for (int i = _slides.Count - 1; i >= 0; i--)
            {
                if (!newOrder.Contains(_slides[i]))
                {
                    _slides.RemoveAt(i);
                }
            }

            for (int i = 0; i < newOrder.Count; i++)
            {
                var slide = newOrder[i];
                int currentIndex = _slides.IndexOf(slide);
                if (currentIndex < 0)
                {
                    _slides.Insert(i, slide);
                }
                else if (currentIndex != i)
                {
                    _slides.Move(currentIndex, i);
                }
            }

            _Slides = newOrder;

            this.RaisePropertyChanged(nameof(Slides));
        }

        public List<Slide> _Slides = new();
        private readonly ObservableCollection<Slide> _slides = new();
        public ObservableCollection<Slide> Slides => _slides;

        private int _selectedSlideIndex = -1;

        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide?.Value;
        }
        
        /// <summary>
        /// mutates *this* SlidesGroupItem and then returns a *new* SlidesGroupItem
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public MediaGroupItemInstance? slice(int start)
        {
            if (start == 0)
            {
                return null;
            }

            MediaGroupItemInstance slidesGroup = new(ParentPlaylist) { Title = $"{Title} (Split copy)" };

            // TODO optimise below to a single loop
            // tricky bit: ensure index logic works whilst removing at the same time

            for (int i = start; i < _Slides.Count; i++)
            {
                slidesGroup._Slides.Add(_Slides[i]);
            }

            var count = _Slides.Count;
            for (int i = start; i < count; i++)
            {
                _Slides.RemoveAt(_Slides.Count - 1);
            }


            return slidesGroup;
        }

        private bool _isDirty = false;
        public bool IsDirty { get => _isDirty; set => this.RaiseAndSetIfChanged(ref _isDirty, value); }
    }
}