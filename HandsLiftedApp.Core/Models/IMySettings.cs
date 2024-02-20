using Avalonia.Controls;

namespace HandsLiftedApp.Core.Models
{
    public interface IMySettings
    {
        string? LastOpenedPlaylistFullPath { get; set; }
        WindowState? LastWindowState { get; set; }

        string AuthClientSecret { get; }
    }
}