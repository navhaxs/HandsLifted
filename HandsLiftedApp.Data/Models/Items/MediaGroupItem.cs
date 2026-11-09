using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.IO;
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
            _items.CollectionItemChanged += _items_CollectionItemChanged;
            ((INotifyPropertyChanged)_autoAdvanceTimer).PropertyChanged += AutoAdvanceTimer_PropertyChanged;
        }

        /// <summary>
        /// Raised when this group's persisted content changes <em>in place</em>: slides added / removed /
        /// moved, a slide's properties edited (media path, caption), or the auto-advance timer edited.
        /// <c>WhenAnyValue(i =&gt; i.Items)</c> can't be used for this - it de-dupes by reference and so
        /// only sees the collection being replaced. Owners (playlist item instances) forward this as
        /// their "item data modified" signal.
        /// </summary>
        public event EventHandler? ContentChanged;

        private void RaiseContentChanged() => ContentChanged?.Invoke(this, EventArgs.Empty);

        // this should be a data type for the XML
        // that is the list of slide media items <by type... video song custom etc>
        [XmlIgnore] public TrulyObservableCollection<GroupItem> _items = new();

        public TrulyObservableCollection<GroupItem> Items
        {
            get => _items;
            set
            {
                // stop listening to the collection being replaced
                _items.CollectionChanged -= _items_CollectionChanged;
                _items.CollectionItemChanged -= _items_CollectionItemChanged;
                this.RaiseAndSetIfChanged(ref _items, value);
                _items.CollectionChanged += _items_CollectionChanged;
                _items.CollectionItemChanged += _items_CollectionItemChanged;
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        private void _items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(Items));
            this.RaisePropertyChanged(nameof(Slides));
            RaiseContentChanged();
        }

        private void _items_CollectionItemChanged(object? sender, PropertyChangedEventArgs e) => RaiseContentChanged();

        private void AutoAdvanceTimer_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseContentChanged();

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
            set
            {
                ((INotifyPropertyChanged)_autoAdvanceTimer).PropertyChanged -= AutoAdvanceTimer_PropertyChanged;
                this.RaiseAndSetIfChanged(ref _autoAdvanceTimer, value);
                ((INotifyPropertyChanged)_autoAdvanceTimer).PropertyChanged += AutoAdvanceTimer_PropertyChanged;
            }
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
            public virtual GroupItem Clone()
            {
                XmlSerializer serializer = new XmlSerializer(this.GetType());
                using (MemoryStream ms = new MemoryStream())
                {
                    serializer.Serialize(ms, this);
                    ms.Position = 0;
                    return (GroupItem)serializer.Deserialize(ms);
                }
            }
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

            public MediaItem()
            {
                ((INotifyPropertyChanged)_meta!).PropertyChanged += Meta_PropertyChanged;
            }

            private MediaItemMeta? _meta = new();

            public MediaItemMeta? Meta
            {
                get => _meta;
                set
                {
                    if (_meta != null) ((INotifyPropertyChanged)_meta).PropertyChanged -= Meta_PropertyChanged;
                    this.RaiseAndSetIfChanged(ref _meta, value);
                    if (_meta != null) ((INotifyPropertyChanged)_meta).PropertyChanged += Meta_PropertyChanged;
                }
            }

            // Editing the caption (Meta.Text) is an edit of this item: surface it as a change of Meta so the
            // owning collection (TrulyObservableCollection.CollectionItemChanged) and group can see it.
            private void Meta_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
                this.RaisePropertyChanged(nameof(Meta));

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