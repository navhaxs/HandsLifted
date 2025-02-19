using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models;

namespace HandsLiftedApp.Core.Utils
{
    public static class PlaylistSaveService
    {
        // displays the 'Save As' file picker dialog, then updates Playlist.PlaylistFilePath
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
                return filePath;
            }

            return null;
        }
    }
}