using HandsLiftedApp.Comparer;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;

namespace HandsLiftedApp.Models.LibraryModel
{
    public class Library : ReactiveObject
    {
        private ObservableCollection<Item> Items { get; }
        private string rootDirectory;
        public ReactiveCommand<Unit, Unit> OnAddSelectedToPlaylistCommand { get; }
        private Item _selectedItem;
        public Item SelectedItem { get => _selectedItem; set => this.RaiseAndSetIfChanged(ref _selectedItem, value); }
        
        private FileSystemWatcher watcher = new FileSystemWatcher();

        public Library() { 
            Items = new ObservableCollection<Item>();
            rootDirectory = "C:\\VisionScreens\\TestSongs";

            OnAddSelectedToPlaylistCommand = ReactiveCommand.Create(() => {
                if (SelectedItem != null) {
                    List<string> items = new List<string>() { SelectedItem.FullFilePath };
                    MessageBus.Current.SendMessage(new AddItemToPlaylistMessage(items));
                }
            });



            Refresh();

            watch();
        }

        void Refresh()
        {
            if (Directory.Exists(rootDirectory))
            {
                var files = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                         .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                // TODO: sync the Items list properly
                foreach (var f in files)
                {
                    Items.Add(new Item() { FullFilePath = f, Title = Path.GetFileNameWithoutExtension(f) });
                }
            }
        }
        private void watch() {
            watcher.Path = rootDirectory;
            watcher.Filter = "*.*";
            watcher.Changed += new FileSystemEventHandler((s, e) => {
                switch (e.ChangeType) {
                   // case WatcherChangeTypes.Created: { }

                }
                Refresh();
            });
            watcher.Created += new FileSystemEventHandler((s, e) => {
                Refresh();
            });
            watcher.Deleted += new FileSystemEventHandler((s, e) => {
                Refresh();
            });
            watcher.Renamed += new RenamedEventHandler((s, e) => {
                Refresh();
            });
            watcher.EnableRaisingEvents = true;
        }
    }

    public class Item
    {
        public string FullFilePath { get; set; }
        public string Title { get; set; } // display title in list view
    }
}
