using Avalonia.Threading;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core
{
    internal class CreateItem
    {
        public static async Task OpenPresentationFileAsync(string path, PlaylistInstance currentPlaylist)
        {
            //try
            //{
            //var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

            //foreach (var path in filePaths)
            //{

            if (path != null && path is string)
            {

                DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(path);

                string targetDirectory = Path.Join(currentPlaylist
                    .PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                if (fileName.EndsWith(".pptx"))
                {
                    Log.Debug($"Importing PowerPoint file: {path}");
                    PowerPointSlidesGroupItem slidesGroup = new PowerPointSlidesGroupItem() { Title = fileName, SourcePresentationFile = path };

                    currentPlaylist.Playlist.Items.Add(slidesGroup);

                    //slidesGroup.SyncState.SyncCommand();
                }
                else if (fileName.EndsWith(".pdf"))
                {
                    Log.Debug($"Importing PDF file: {path}");
                    PDFSlidesGroupItem slidesGroup = new PDFSlidesGroupItem() { Title = fileName, SourcePresentationFile = path };

                    currentPlaylist.Playlist.Items.Add(slidesGroup);

                    //ConvertPDF.Convert(path, targetDirectory); // todo move into State as a SyncCommand
                    //PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, targetDirectory);
                }
                else
                {
                    Log.Error($"Unsupported file type: {path}");
                    return;
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // wait for UI to update...
                    Dispatcher.UIThread.RunJobs();
                    // and now we can jump to view
                    var count = currentPlaylist.Playlist.Items.Count;
                    MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                });
            }
            //    }
            //}
            //catch (Exception e)
            //{
            //    Debug.Print(e.Message);
            //}
        }

    }
}
