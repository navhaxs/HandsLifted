using Avalonia.Controls;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.LibraryModel
{
    public class Library : ReactiveObject
    {
        private ObservableCollection<Item> Items { get; }

        private string rootDirectory;
        public ReactiveCommand<Unit, Unit> OnAddSelectedToPlaylistCommand { get; }
        private Item _selectedItem;
        public Item SelectedItem { get => _selectedItem; set => this.RaiseAndSetIfChanged(ref _selectedItem, value); }

        private ObservableAsPropertyHelper<string> _selectedItemPreview;
        public string SelectedItemPreview { get => _selectedItemPreview.Value; }

        private FileSystemWatcher watcher = new FileSystemWatcher();

        public Library()
        {
            if (Design.IsDesignMode)
            {
                Items = new ObservableCollection<Item>();
                return;
            }

            Globals.Preferences.WhenAnyValue(prefs => prefs.LibraryPath).Subscribe(libraryPath =>
            {
                rootDirectory = libraryPath;
                Refresh();
                watch();
            });
            _selectedItemPreview = this.WhenAnyValue(x => x.SelectedItem, (_SelectedItem) =>
                  {
                      if (_SelectedItem != null)
                      {
                          try
                          {
                              return File.ReadAllText(_SelectedItem.FullFilePath); ;
                          }
                          catch (Exception ex)
                          {
                              Log.Error("Attempt to read file for library preview failed", ex);
                          }
                      }
                      return null;
                  })
                  .ToProperty(this, c => c.SelectedItemPreview)
                  ;
            Items = new ObservableCollection<Item>();
            //rootDirectory = Globals.Env.SongLibraryDirectory;
            //rootDirectory = Globals.Env.SongLibraryDirectory;

            OnAddSelectedToPlaylistCommand = ReactiveCommand.Create(() =>
            {
                if (SelectedItem != null)
                {
                    List<string> items = new List<string>() { SelectedItem.FullFilePath };
                    MessageBus.Current.SendMessage(new AddItemToPlaylistMessage(items));
                }
            });

            Refresh();

            watch();

            _searchResults = this
                .WhenAnyValue(x => x.SearchTerm)
                //.Throttle(TimeSpan.FromMilliseconds(100))
                .Select(term => term?.Trim().ToLower())
                .DistinctUntilChanged()
                //.Where(term => !string.IsNullOrWhiteSpace(term))
                .SelectMany(SearchLibrary)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SearchResults);

            // We subscribe to the "ThrownExceptions" property of our OAPH, where ReactiveUI 
            // marshals any exceptions that are thrown in SearchNuGetPackages method. 
            // See the "Error Handling" section for more information about this.
            _searchResults.ThrownExceptions.Subscribe(error => { /* Handle errors here */ });

            // A helper method we can use for Visibility or Spinners to show if results are available.
            // We get the latest value of the SearchResults and make sure it's not null.
            _isAvailable = this
                .WhenAnyValue(x => x.SearchResults)
                .Select(searchResults => searchResults != null)
                .ToProperty(this, x => x.IsAvailable);
        }

        // In ReactiveUI, this is the syntax to declare a read-write property
        // that will notify Observers, as well as WPF, that a property has 
        // changed. If we declared this as a normal property, we couldn't tell 
        // when it has changed!
        private string _searchTerm;
        public string SearchTerm
        {
            get => _searchTerm;
            set => this.RaiseAndSetIfChanged(ref _searchTerm, value);
        }

        // Here's the interesting part: In ReactiveUI, we can take IObservables
        // and "pipe" them to a Property - whenever the Observable yields a new
        // value, we will notify ReactiveObject that the property has changed.
        // 
        // To do this, we have a class called ObservableAsPropertyHelper - this
        // class subscribes to an Observable and stores a copy of the latest value.
        // It also runs an action whenever the property changes, usually calling
        // ReactiveObject's RaisePropertyChanged.
        private readonly ObservableAsPropertyHelper<IEnumerable<Item>> _searchResults;
        public IEnumerable<Item> SearchResults => _searchResults.Value;

        // Here, we want to create a property to represent when the application 
        // is performing a search (i.e. when to show the "spinner" control that 
        // lets the user know that the app is busy). We also declare this property
        // to be the result of an Observable (i.e. its value is derived from 
        // some other property)
        private readonly ObservableAsPropertyHelper<bool> _isAvailable;
        public bool IsAvailable => _isAvailable.Value;

        private async Task<IEnumerable<Item>> SearchLibrary(
            string term, CancellationToken token)
        {
            if (term == null || term.Length == 0)
                return Items;

            // TODO: filter by file *content* as well (full-text search)
            return Items.Where(item => item.Title.ToLower().Contains(term));
        }

        void Refresh()
        {
            if (Directory.Exists(rootDirectory) && Items != null)
            {
                var files = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                         .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                Log.Information($"Refreshed library at {rootDirectory}");
                Items.Clear();

                // TODO: sync the Items list properly
                foreach (var f in files)
                {
                    Items.Add(new Item() { FullFilePath = f, Title = Path.GetFileNameWithoutExtension(f) });
                }
            }
        }
        private void watch()
        {
            if (!Directory.Exists(rootDirectory))
                return;

            watcher = new FileSystemWatcher();

            watcher.Path = rootDirectory;
            watcher.Filter = "*.*";
            watcher.Changed += new FileSystemEventHandler((s, e) =>
            {
                switch (e.ChangeType)
                {
                    // case WatcherChangeTypes.Created: { }

                }
                Refresh();
            });
            watcher.Created += new FileSystemEventHandler((s, e) =>
            {
                Refresh();
            });
            watcher.Deleted += new FileSystemEventHandler((s, e) =>
            {
                Refresh();
            });
            watcher.Renamed += new RenamedEventHandler((s, e) =>
            {
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
