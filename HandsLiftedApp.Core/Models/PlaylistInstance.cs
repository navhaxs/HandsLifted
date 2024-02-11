using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models
{
    public class PlaylistInstance : Playlist
    {
        public PlaylistInstance()
        {
            // _selectedItem = Observable.CombineLatest(
            //     this.WhenAnyValue(x => x.SelectedItemIndex),
            //     Playlist.Items.ObserveCollectionChanges(),
            //     (selectedIndex, items) =>
            //     {
            //         if (selectedIndex != -1)
            //         {
            //             return Playlist.Items.ElementAtOrDefault(selectedIndex);
            //         }
            //
            //         return new BlankItem();
            //     }
            //     
            //     
            // )   
            //     
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

                        return new BlankItemInstance();
                    })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedItemAsIItemInstance);

            _activeSlide = this.WhenAnyValue(x => x.SelectedItemAsIItemInstance.ActiveSlide, (Slide activeSlide) =>
                {
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
                            dest.SlideTheme = source.SlideTheme;
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
    }
}