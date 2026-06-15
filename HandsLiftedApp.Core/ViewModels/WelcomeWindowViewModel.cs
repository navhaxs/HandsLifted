using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class WelcomeWindowViewModel : ReactiveObject
    {
        public MainViewModel _parent;

        public WelcomeWindowViewModel()
        {
            if (Design.IsDesignMode)
            {
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Sunday Service.hlsx", FilePath = @"C:\Playlists\Sunday Service.hlsx", LastOpenedDate = new DateTime(2026, 6, 8) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Youth Night.hlsx", FilePath = @"C:\Playlists\Youth Night.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Wedding.hlsx", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
            }
        }

        public WelcomeWindowViewModel(MainViewModel parent) : this()
        {
            _parent = parent;

            if (parent?.settings?.RecentPlaylistFullPathsList is not null)
            {
                RecentPlaylists.Clear();
                foreach (var se in parent.settings.RecentPlaylistFullPathsList)
                {
                    DateTime? lastOpened = File.Exists(se) ? File.GetLastWriteTime(se) : null;
                    RecentPlaylists.Add(new RecentPlaylistEntry() { FilePath = se, FileName = Path.GetFileName(se), LastOpenedDate = lastOpened });
                }
            }
        }

        private List<RecentPlaylistEntry> _recentPlaylists = new();

        public List<RecentPlaylistEntry> RecentPlaylists
        {
            get => _recentPlaylists;
            set => this.RaiseAndSetIfChanged(ref _recentPlaylists, value);
        }

        public class RecentPlaylistEntry
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public DateTime? LastOpenedDate { get; set; }
            public string LastOpenedDateText => LastOpenedDate?.ToString("d MMM yyyy") ?? string.Empty;
        }
    }
}