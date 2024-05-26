using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Models.Library
{
    public class Library : ReactiveObject
    {
        public string Label
        {
            get => Config.Label;
        }

        public ObservableCollection<Item> Items { get; }

        private LibraryConfig.LibraryDefinition Config;

        private FileSystemWatcher watcher = new FileSystemWatcher();

        public Library(LibraryConfig.LibraryDefinition config)
        {
            Config = config;
            
            if (Design.IsDesignMode)
            {
                Items = new ObservableCollection<Item>();
                return;
            }

            // Globals.AppPreferences.WhenAnyValue(prefs => prefs.LibraryPath).Subscribe(libraryPath =>
            // {
            //     rootDirectory = libraryPath;
            //     Refresh();
            //     watch();
            // });
            Items = new ObservableCollection<Item>();
            // rootDirectory = Globals.AppPreferences.LibraryPath;

          

            Refresh();

            // watch();


        }

   

        void Refresh()
        {
            if (Directory.Exists(Config.Directory) && Items != null)
            {
                var files = Directory.GetFiles(Config.Directory, "*.*", SearchOption.AllDirectories)
                         .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                Log.Information($"Refreshed library [{Config.Label}] {Config.Directory}");
                Items.Clear();

                // TODO: sync the Items list properly
                foreach (var f in files)
                {
                    Items.Add(new Item() { FullFilePath = f, Title = Path.GetFileNameWithoutExtension(f) });
                }
            }
        }
        // private void watch()
        // {
        //     if (!Directory.Exists(rootDirectory))
        //         return;
        //
        //     watcher = new FileSystemWatcher();
        //
        //     watcher.Path = rootDirectory;
        //     watcher.Filter = "*.*";
        //     watcher.Changed += new FileSystemEventHandler((s, e) =>
        //     {
        //         switch (e.ChangeType)
        //         {
        //             // case WatcherChangeTypes.Created: { }
        //
        //         }
        //         Refresh();
        //     });
        //     watcher.Created += new FileSystemEventHandler((s, e) =>
        //     {
        //         Refresh();
        //     });
        //     watcher.Deleted += new FileSystemEventHandler((s, e) =>
        //     {
        //         Refresh();
        //     });
        //     watcher.Renamed += new RenamedEventHandler((s, e) =>
        //     {
        //         Refresh();
        //     });
        //     watcher.EnableRaisingEvents = true;
        // }
    }

    public class Item
    {
        public string FullFilePath { get; set; }
        public string Title { get; set; } // display title in list view
    }
}
