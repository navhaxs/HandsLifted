using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class AddItemPlaceholder
    {
        // Placeholder class to represent the "Add new slide" button in the UI
    }

    public class MediaGroupItemInstance : MediaGroupItem, IItemInstance, IItemDirtyBit {
        public PlaylistInstance ParentPlaylist { get; set; }
        
        public event EventHandler ItemDataModified;

        private BlankSlide _blankSlide = new();

        public MediaGroupItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
            // TODO do not use SelectedSlideIndex, rather use slide id ref ! currently this causes a flicker when re-ordering slides
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
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            // Set up reactive ItemsWithAddButton property that observes collection changes
            _itemsWithAddButton = this.WhenAnyValue(x => x.Items)
                .Select(items => 
                {
                    if (items == null)
                        return Observable.Return(new[] { new AddItemPlaceholder() }.AsEnumerable());
                    
                    return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                        h => items.CollectionChanged += h,
                        h => items.CollectionChanged -= h)
                        .Select(_ => items.Cast<object>().Concat(new[] { new AddItemPlaceholder() }))
                        .StartWith(items.Cast<object>().Concat(new[] { new AddItemPlaceholder() }));
                })
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ItemsWithAddButton);

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

        private ObservableAsPropertyHelper<IEnumerable<object>> _itemsWithAddButton;

        public IEnumerable<object> ItemsWithAddButton => _itemsWithAddButton.Value;

        public void GenerateSlides()
        {
            var x = new List<Slide>();
            foreach (var item in Items)
            {
                var generateMediaContentSlide = CreateItem.GenerateMediaContentSlide(item, this);
                x.Add(generateMediaContentSlide);
            }

            _Slides = x;

            this.RaisePropertyChanged(nameof(Slides));
        }

        public List<Slide> _Slides = new();
        public ObservableCollection<Slide> Slides => new(_Slides);

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