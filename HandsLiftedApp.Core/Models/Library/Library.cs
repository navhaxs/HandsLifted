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

        public ObservableCollection<LibraryItem> Items { get; }

        public LibraryConfig.LibraryDefinition Config { get; init; }

        private FileSystemWatcher watcher = new FileSystemWatcher();

        private bool _isMediaBin;

        public bool isMediaBin
        {
            get => _isMediaBin;
            set => this.RaiseAndSetIfChanged(ref _isMediaBin, value);
        }

        public Library(LibraryConfig.LibraryDefinition config)
        {
            Config = config;

            if (Design.IsDesignMode)
            {
                Items = new ObservableCollection<LibraryItem>();
                return;
            }

            // Globals.AppPreferences.WhenAnyValue(prefs => prefs.LibraryPath).Subscribe(libraryPath =>
            // {
            //     rootDirectory = libraryPath;
            //     Refresh();
            //     watch();
            // });
            Items = new ObservableCollection<LibraryItem>();
            // rootDirectory = Globals.AppPreferences.LibraryPath;

            Refresh();

            // watch();
        }

        void Refresh()
        {
            if (Directory.Exists(Config.Directory) && Items != null)
            {
                var files = new DirectoryInfo(Config.Directory).GetFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .Select(f => f.FullName)
                    // .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                Log.Information($"Refreshed library [{Config.Label}] [{Config.Directory}]");
                Items.Clear();

                // TODO: sync the Items list properly
                foreach (var f in files)
                {
                    Items.Add(new LibraryItem() { FullFilePath = f });
                }

                isMediaBin = !(Items.Count > 0 && (Items.First().FullFilePath.ToLower().EndsWith("txt") ||
                                                   Items.First().FullFilePath.ToLower().EndsWith("xml")));
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

    public class LibraryItem : ReactiveObject
    {
        public LibraryItem()
        {
            _title = this.WhenAnyValue(x => x.FullFilePath)
                .Select(a => Path.GetFileName(a))
                .ToProperty(this, x => x.Title, out _title);
        }

        private string _fullFilePath;
        public string FullFilePath { get => _fullFilePath; set => this.RaiseAndSetIfChanged(ref _fullFilePath, value); }

        private ObservableAsPropertyHelper<string> _title;
        // display title in list view
        public string Title { get => _title.Value; } 
    }
}