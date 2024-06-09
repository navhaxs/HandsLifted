using System.IO;
using HandsLiftedApp.Core.Models;

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

            HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, playlist.PlaylistFilePath);
        }

        public static string GetAutoSavePlaylistFilePath(string fullPath)
        {
            var filename = Path.GetFileName(fullPath);
            var extension = Path.GetExtension(fullPath);
            return filename + ".autosave" + "." + extension;
        }

        public static void AutoSaveDocument(PlaylistInstance playlist)
        {
            if (playlist.PlaylistFilePath == null)
            {
                return;
            }
            
            HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, GetAutoSavePlaylistFilePath(playlist.PlaylistFilePath));
        }
    }
}