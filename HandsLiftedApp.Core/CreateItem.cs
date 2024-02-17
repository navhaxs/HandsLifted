using Avalonia.Threading;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core
{
    internal class CreateItem
    {
        public static readonly string[] SUPPORTED_SONG = { "txt", "xml" };
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        public static readonly string[] SUPPORTED_VIDEO = { "mp4", "flv", "mov", "mkv", "avi", "wmv" };
        public static readonly string[] SUPPORTED_IMAGE = { "bmp", "png", "jpg", "jpeg" };
        
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

                    currentPlaylist.Items.Add(slidesGroup);

                    //slidesGroup.SyncState.SyncCommand();
                }
                else if (fileName.EndsWith(".pdf"))
                {
                    Log.Debug($"Importing PDF file: {path}");
                    PDFSlidesGroupItem slidesGroup = new PDFSlidesGroupItem() { Title = fileName, SourcePresentationFile = path };

                    currentPlaylist.Items.Add(slidesGroup);

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
                    var count = currentPlaylist.Items.Count;
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

        public static MediaSlide GenerateMediaContentSlide(string filePath) //, int index)
        {
            string _filename = filePath.ToLower();

            // TODO: make VideoSlide and ImageSlide both share common MediaSlide parent class
            if (SUPPORTED_VIDEO.Any(x => _filename.EndsWith(x)))
            {
                return new VideoSlideInstance(filePath); // { Index = index };
            }

            return new ImageSlide(filePath); // { Index = index };
        }
    }
}
