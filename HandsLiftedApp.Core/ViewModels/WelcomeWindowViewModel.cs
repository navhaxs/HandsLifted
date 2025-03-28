using System.Collections.Generic;
using System.IO;
using System.Reactive;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class WelcomeWindowViewModel : ReactiveObject
    {
        public MainViewModel _parent;

        public WelcomeWindowViewModel(MainViewModel parent)
        {
            _parent = parent;

            if (parent.settings.RecentPlaylistFullPathsList is not null)
            {
                foreach (var se in parent.settings.RecentPlaylistFullPathsList)
                {
                    RecentPlaylists.Add(new RecentPlaylistEntry() { FilePath = se, FileName = Path.GetFileName(se) });
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
            // date last modified
        }
    }
}