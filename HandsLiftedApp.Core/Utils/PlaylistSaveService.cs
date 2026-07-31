using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models;

namespace HandsLiftedApp.Core.Utils
{
    public static class PlaylistSaveService
    {
        // displays the 'Save As' file picker dialog, then updates Playlist.PlaylistFilePath
        // and Playlist.PlaylistWorkingDirectory
        public static async Task<string?> ShowSaveAsDialog(Control sender, PlaylistInstance playlist)
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(sender);

            // Start async operation to open the dialog.
            var xmlFileType = new FilePickerFileType("XML Document")
            {
                Patterns = new[] { "*.xml" },
                MimeTypes = new[] { "text/xml" }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File",
                FileTypeChoices = new[] { xmlFileType }
            });

            if (file != null)
            {
                var filePath = file.Path.LocalPath;
                playlist.PlaylistFilePath = filePath;

                // Keep the working directory in step with the file path, the same way the
                // playlist-load path does (MainViewModel). Without this, PortableAssetCopier
                // would keep resolving copy-into-playlist-folder destinations against the
                // class-default relative path for the rest of the authoring session.
                var playlistDirectoryPath = Path.GetDirectoryName(filePath);
                if (playlistDirectoryPath != null)
                {
                    playlist.PlaylistWorkingDirectory = playlistDirectoryPath;
                }

                return filePath;
            }

            return null;
        }
    }
}