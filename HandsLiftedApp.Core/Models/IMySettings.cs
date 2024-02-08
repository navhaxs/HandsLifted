namespace HandsLiftedApp.Core.Models
{
    public interface IMySettings
    {
        string? LastOpenedPlaylistFullPath { get; set; }

        string AuthClientSecret { get; }
    }
}