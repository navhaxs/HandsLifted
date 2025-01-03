using System;
using System.Collections.Specialized;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("MediaGroupItem", Namespace = Constants.Namespace, IsNullable = false)]
    [XmlInclude(typeof(MediaItem))]
    [XmlInclude(typeof(SlideItem))]
    [Serializable]
    public class MediaGroupItem : Item
    {
        public MediaGroupItem()
        {
            _items.CollectionChanged += _items_CollectionChanged;
        }

        // this should be a data type for the XML
        // that is the list of slide media items <by type... video song custom etc>
        [XmlIgnore] public TrulyObservableCollection<GroupItem> _items = new();

        public TrulyObservableCollection<GroupItem> Items
        {
            get => _items;
            set
            {
                this.RaiseAndSetIfChanged(ref _items, value);
                _items.CollectionChanged += _items_CollectionChanged;
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        private void _items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(Items));
            this.RaisePropertyChanged(nameof(Slides));
        }

        private bool _IsLooping = false;

        /// <summary>
        /// Loop back to the first slide of the item once reaching the end 
        /// </summary>
        public bool IsLooping
        {
            get => _IsLooping;
            set => this.RaiseAndSetIfChanged(ref _IsLooping, value);
        }

        private ItemAutoAdvanceTimer _autoAdvanceTimer = new();

        public ItemAutoAdvanceTimer AutoAdvanceTimer
        {
            get => _autoAdvanceTimer;
            set => this.RaiseAndSetIfChanged(ref _autoAdvanceTimer, value);
        }

        /// <summary>
        /// mutates *this* SlidesGroupItem and then returns a *new* SlidesGroupItem
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public MediaGroupItem? Slice(int start)
        {
            if (start == 0)
            {
                return null;
            }

            MediaGroupItem slidesGroup = new MediaGroupItem { Title = $"{Title} (Split copy)" };

            // TODO optimise below to a single loop
            // tricky bit: ensure index logic works whilst removing at the same time

            for (int i = start; i < Items.Count; i++)
            {
                slidesGroup.Items.Add(Items[i]);
            }

            var count = Items.Count;
            for (int i = start; i < count; i++)
            {
                Items.RemoveAt(Items.Count - 1);
            }

            return slidesGroup;
        }

        [XmlRoot(Namespace = Constants.Namespace)]
        [XmlInclude(typeof(MediaItem))]
        [XmlInclude(typeof(SlideItem))]
        [Serializable]
        public abstract class GroupItem : ReactiveObject
        {
        }

        [XmlRoot(Namespace = Constants.Namespace)]
        [Serializable]
        public class MediaItem : GroupItem
        {
            private string? _sourceMediaFilePath;

            public string? SourceMediaFilePath
            {
                get => _sourceMediaFilePath;
                set => this.RaiseAndSetIfChanged(ref _sourceMediaFilePath, value);
            }

            private MediaItemMeta? _meta = new();

            public MediaItemMeta? Meta
            {
                get => _meta;
                set => this.RaiseAndSetIfChanged(ref _meta, value);
            }

            [XmlRoot(Namespace = Constants.Namespace)]
            [Serializable]
            public class MediaItemMeta : ReactiveObject
            {
                private string? _text;

                public string? Text
                {
                    get => _text;
                    set => this.RaiseAndSetIfChanged(ref _text, value);
                }
            }
        }

        [XmlRoot(Namespace = Constants.Namespace)]
        [Serializable]
        public class SlideItem : GroupItem
        {
            private CustomSlide _slideData = new();

            public CustomSlide SlideData
            {
                get => _slideData;
                set => this.RaiseAndSetIfChanged(ref _slideData, value);
            }
        }
    }
}