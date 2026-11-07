using Avalonia.Controls;
using Config.Net;

namespace HandsLiftedApp.Core.Models
{
    public interface IMySettings
    {
        string[]? RecentPlaylistFullPathsList { get; set; }
        WindowState? LastWindowState { get; set; }

        [Option(DefaultValue = true)]
        bool AutoSaveEnabled { get; set; }

        string AuthClientSecret { get; }
    }
}