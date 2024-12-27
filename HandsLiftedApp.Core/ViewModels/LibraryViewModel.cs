using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views.Editors.Song;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HandsLiftedApp.Core.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OnAddSelectedToPlaylistCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateNewSongCommand { get; }
        private ObservableAsPropertyHelper<string> _selectedItemPreview;

        public string SelectedItemPreview
        {
            get => _selectedItemPreview.Value;
        }
        
        private bool _isPreviewLoading = false;

        public bool IsPreviewLoading
        {
            get => _isPreviewLoading;
            set => this.RaiseAndSetIfChanged(ref _isPreviewLoading, value);
        }

        private Bitmap? _selectedItemImagePreview;

        public Bitmap? SelectedItemImagePreview
        {
            get => _selectedItemImagePreview;
            set => this.RaiseAndSetIfChanged(ref _selectedItemImagePreview, value);
        }

        private LibraryConfig _libraryConfig = new LibraryConfig();

        public LibraryConfig LibraryConfig
        {
            get => _libraryConfig;
            set => this.RaiseAndSetIfChanged(ref _libraryConfig, value);
        }

        private CancellationTokenSource _cancellationTokenSource;

        public void ReloadLibraries()
        {
            var lastSavedConfig = LoadConfig();
            if (lastSavedConfig == null || lastSavedConfig.LibraryItems.Count == 0)
            {
                // config is for loading...
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                    { Label = "Songs", Directory = @"D:\data\VisionScreens\Song Library" });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Church News",
                    Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Church News"
                });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Sermon Media",
                    Directory =
                        @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Sermon Media"
                });
                LibraryConfig.LibraryItems.Add(new LibraryConfig.LibraryDefinition()
                {
                    Label = "Kids Talks",
                    Directory = @"H:\.shortcut-targets-by-id\1VCRKC34SblCDK3hzr9hkb7b5wMjUQF5R\Service Docs\Kids Talks"
                });
                WriteConfig();
            }
            else
            {
                LibraryConfig = lastSavedConfig;
            }

            // runtime...
            Libraries = new ObservableCollection<Library>();

            foreach (var library in LibraryConfig.LibraryItems)
            {
                Libraries.Add(new Library(library));
            }
        }

        public LibraryViewModel()
        {
            ReloadLibraries();

            _cancellationTokenSource = new CancellationTokenSource();

            OnAddSelectedToPlaylistCommand = ReactiveCommand.Create(() =>
            {
                // if (ActiveQuery != null)
                // {
                //     List<string> items = new List<string>() { ActiveQuery.FullFilePath };
                //     MessageBus.Current.SendMessage(new AddItemByFilePathMessage(items));
                // }
            });

            CreateNewSongCommand = ReactiveCommand.Create(() =>
            {
                // TODO remove dependency on Globals.MainViewModel.Playlist 
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new SongEditorWindow(), false,
                    new SongEditorViewModel(new SongItemInstance(null), Globals.Instance.MainViewModel.Playlist)));
            });

            if (Design.IsDesignMode)
            {
                SelectedLibrary = Libraries.First();
            }

            this.WhenAnyValue(t => t.SelectedLibrary).Subscribe(x =>
            {
                if (x == null)
                {
                    ActiveQuery = null;
                }
                else
                {
                    ActiveQuery = new LibraryQueryViewModel(new List<Library>(){x});
                }                
            });
        }

        private void WriteConfig()
        {
            try
            {
                if (Design.IsDesignMode) return;
                
                using (var streamWriter = new StreamWriter(Constants.LIBRARY_CONFIG_FILEPATH))
                {
                    var serializer = new SerializerBuilder()
                        // .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    serializer.Serialize(streamWriter, LibraryConfig);
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to write library config", e);
                throw;
            }
        }

        private LibraryConfig? LoadConfig()
        {
            try
            {
                if (Design.IsDesignMode) return null;
                
                using (var streamReader = new StreamReader(Constants.LIBRARY_CONFIG_FILEPATH))
                {
                    var deserializer = new DeserializerBuilder()
                        // .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    return deserializer.Deserialize<LibraryConfig>(streamReader);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load library config", ex);
                return null;
            }
        }

        private LibraryQueryViewModel? _activeQuery;

        public LibraryQueryViewModel? ActiveQuery
        {
            get => _activeQuery;
            set => this.RaiseAndSetIfChanged(ref _activeQuery, value);
        }

        private ObservableCollection<Library> _libraries;
        public ObservableCollection<Library> Libraries { get => _libraries ; set => this.RaiseAndSetIfChanged(ref _libraries, value); }

        private Library _selectedLibrary;

        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set => this.RaiseAndSetIfChanged(ref _selectedLibrary, value);
        }
    }
}