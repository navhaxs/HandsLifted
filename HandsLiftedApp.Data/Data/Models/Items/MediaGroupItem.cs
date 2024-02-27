using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("MediaGroupItem", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class MediaGroupItem : Item
    {
        public MediaGroupItem()
        {
            _items.CollectionChanged += _items_CollectionChanged;
        }

        // this should be a data type for the XML
        // that is the list of slide media items <by type... video song custom etc>
        [XmlIgnore]
        public TrulyObservableCollection<MediaItem> _items = new();
        public TrulyObservableCollection<MediaItem> Items
        {
            get => _items; set
            {
                this.RaiseAndSetIfChanged(ref _items, value);
                _items.CollectionChanged += _items_CollectionChanged;
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        private void _items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(Items));
            this.RaisePropertyChanged(nameof(Slides));
        }

        private bool _IsLooping = false;

        /// <summary>
        /// Loop back to the first slide of the item once reaching the end 
        /// </summary>
        public bool IsLooping { get => _IsLooping; set => this.RaiseAndSetIfChanged(ref _IsLooping, value); }

        private ItemAutoAdvanceTimer _autoAdvanceTimer = new();
        public ItemAutoAdvanceTimer AutoAdvanceTimer { get => _autoAdvanceTimer; set => this.RaiseAndSetIfChanged(ref _autoAdvanceTimer, value); }

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

            MediaGroupItem slidesGroup = new MediaGroupItem{ Title = $"{Title} (Split copy)" };

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
        [Serializable]
        public class MediaItem : ReactiveObject
        {
            private string? _sourceMediaFilePath;
            public string? SourceMediaFilePath { get => _sourceMediaFilePath; set => this.RaiseAndSetIfChanged(ref _sourceMediaFilePath, value); }
        }
    }
}
