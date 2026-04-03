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

            DeleteAutoSave(playlist.PlaylistFilePath);
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

        public static bool IsAutoSaveNewer(string playlistFilePath)
        {
            if (string.IsNullOrEmpty(playlistFilePath)) return false;

            string autosavePath = GetAutoSavePlaylistFilePath(playlistFilePath);
            if (!File.Exists(autosavePath)) return false;

            var playlistFileInfo = new FileInfo(playlistFilePath);
            var autosaveFileInfo = new FileInfo(autosavePath);

            return autosaveFileInfo.LastWriteTime > playlistFileInfo.LastWriteTime;
        }

        public static void DeleteAutoSave(string? playlistFilePath)
        {
            if (string.IsNullOrEmpty(playlistFilePath)) return;

            string autosavePath = GetAutoSavePlaylistFilePath(playlistFilePath);
            if (File.Exists(autosavePath))
            {
                Log.Information("Deleting autosave file {AutoSavePath}", autosavePath);
                File.Delete(autosavePath);
            }
        }
    }
}