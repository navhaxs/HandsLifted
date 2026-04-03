using System.IO;
using HandsLiftedApp.Core.Models;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public static class PlaylistDocumentService
    {
        public static void SaveDocument(PlaylistInstance playlist)
        {
            if (playlist.PlaylistFilePath == null)
            {
                throw new FileNotFoundException("Playlist cannot be saved. PlaylistFilePath is null.");
            }

            Log.Information("Saving playlist to {FilePath}", playlist.PlaylistFilePath);
            HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, playlist.PlaylistFilePath);
            playlist.IsDirty = false;

            string autosavePath = GetAutoSavePlaylistFilePath(playlist.PlaylistFilePath);
            if (File.Exists(autosavePath))
            {
                File.Delete(autosavePath);
            }
        }

        public static string GetAutoSavePlaylistFilePath(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            var extension = Path.GetExtension(fullPath);
            return Path.Combine(directory ?? string.Empty, filenameWithoutExtension + ".autosave" + extension);
        }

        public static void AutoSaveDocument(PlaylistInstance playlist)
        {
            if (string.IsNullOrEmpty(playlist.PlaylistFilePath)) return;

            string autosavePath = GetAutoSavePlaylistFilePath(playlist.PlaylistFilePath);
            Log.Information("Autosaving playlist to {AutoSavePath}", autosavePath);
            HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, autosavePath);
            // Do NOT set IsDirty = false here, so the user still knows they have unsaved changes
        }
    }
}