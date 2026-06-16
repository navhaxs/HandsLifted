using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Something with a really, really, really, really, really, really long name", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Something with a really, really, really, really, really, really long name", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Something with a really, really, really, really, really, really long name", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Something with a really, really, really, really, really, really long name", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
                RecentPlaylists.Add(new RecentPlaylistEntry() { FileName = "Something with a really, really, really, really, really, really long name", FilePath = @"C:\Playlists\Wedding.hlsx", LastOpenedDate = new DateTime(2026, 5, 25) });
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

        private ObservableCollection<RecentPlaylistEntry> _recentPlaylists = new();

        public ObservableCollection<RecentPlaylistEntry> RecentPlaylists
        {
            get => _recentPlaylists;
            set => this.RaiseAndSetIfChanged(ref _recentPlaylists, value);
        }

        public class RecentPlaylistEntry
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public DateTime? LastOpenedDate { get; set; }
            public string LastOpenedDateText
            {
                get
                {
                    if (LastOpenedDate is null) return string.Empty;
                    var days = (DateTime.Now - LastOpenedDate.Value).TotalDays;
                    if (days < 1) return "today";
                    if (days < 2) return "yesterday";
                    if (days < 7) return $"{(int)days} days ago";
                    if (days < 14) return "1 week ago";
                    if (days < 30) return $"{(int)(days / 7)} weeks ago";
                    if (days < 60) return "1 month ago";
                    if (days < 365) return $"{(int)(days / 30)} months ago";
                    return $"{(int)(days / 365)} year{((int)(days / 365) > 1 ? "s" : "")} ago";
                }
            }
        }
    }
}