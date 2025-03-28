using Avalonia.Controls;

namespace HandsLiftedApp.Core.Models
{
    public interface IMySettings
    {
        string[]? RecentPlaylistFullPathsList { get; set; }
        WindowState? LastWindowState { get; set; }

        string AuthClientSecret { get; }
    }
}