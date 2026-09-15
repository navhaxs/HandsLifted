using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Core.Models.Library;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels
{
    public class MediaLibraryQueryViewModel : ReactiveObject
    {
        public Library Library { get; }
        public string RootPath => Library.Config.Directory;

        private string _currentRelativePath = "";
        public string CurrentRelativePath
        {
            get => _currentRelativePath;
            private set => this.RaiseAndSetIfChanged(ref _currentRelativePath, value);
        }

        private string _searchTerm = "";
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchTerm, value);
                ApplyFilter();
            }
        }

        public ObservableCollection<HomeLibraryEntry> Entries { get; } = new();
        public ObservableCollection<BreadcrumbSegment> Breadcrumbs { get; } = new();

        public ReactiveCommand<HomeLibraryEntry, Unit> NavigateIntoCommand { get; }
        public ReactiveCommand<string, Unit> NavigateToBreadcrumbCommand { get; }

        private List<HomeLibraryEntry> _allEntriesInCurrentFolder = new();

        public MediaLibraryQueryViewModel(Library library)
        {
            Library = library;

            NavigateIntoCommand = ReactiveCommand.Create<HomeLibraryEntry>(entry =>
            {
                if (entry.IsDirectory)
                {
                    Navigate(string.IsNullOrEmpty(CurrentRelativePath)
                        ? entry.Title
                        : Path.Combine(CurrentRelativePath, entry.Title));
                }
            });

            NavigateToBreadcrumbCommand = ReactiveCommand.Create<string>(Navigate);

            Navigate("");
        }

        private void Navigate(string relativePath)
        {
            CurrentRelativePath = relativePath ?? "";
            RescanCurrentFolder();
            RebuildBreadcrumbs();
        }

        private void RescanCurrentFolder()
        {
            var currentDir = string.IsNullOrEmpty(CurrentRelativePath)
                ? RootPath
                : Path.Combine(RootPath, CurrentRelativePath);

            var entries = new List<HomeLibraryEntry>();

            try
            {
                if (Directory.Exists(currentDir))
                {
                    var comparer = new NaturalSortStringComparer(StringComparison.Ordinal);

                    var folders = new DirectoryInfo(currentDir).GetDirectories()
                        .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                        .OrderBy(d => d.Name, comparer)
                        .Select(d => new HomeLibraryEntry { FullPath = d.FullName, Title = d.Name, IsDirectory = true });

                    var files = new DirectoryInfo(currentDir).GetFiles()
                        .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                        .Where(f => Library.SupportedMediaExtensions.Contains(f.Extension.TrimStart('.')))
                        .OrderBy(f => f.Name, comparer)
                        .Select(f => new HomeLibraryEntry { FullPath = f.FullName, Title = f.Name, IsDirectory = false });

                    entries.AddRange(folders);
                    entries.AddRange(files);
                }
                else
                {
                    Log.Warning("Media library folder [{Directory}] does not exist", currentDir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan media library folder [{Directory}]", currentDir);
            }

            _allEntriesInCurrentFolder = entries;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<HomeLibraryEntry> filtered = string.IsNullOrWhiteSpace(SearchTerm)
                ? _allEntriesInCurrentFolder
                : _allEntriesInCurrentFolder.Where(e =>
                    e.Title.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));

            Entries.Clear();
            foreach (var entry in filtered)
            {
                Entries.Add(entry);
            }
        }

        private void RebuildBreadcrumbs()
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbSegment(Library.Label, ""));

            if (string.IsNullOrEmpty(CurrentRelativePath))
            {
                return;
            }

            var segments = CurrentRelativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var accumulated = "";
            foreach (var segment in segments)
            {
                accumulated = string.IsNullOrEmpty(accumulated) ? segment : Path.Combine(accumulated, segment);
                Breadcrumbs.Add(new BreadcrumbSegment(segment, accumulated));
            }
        }
    }
}
