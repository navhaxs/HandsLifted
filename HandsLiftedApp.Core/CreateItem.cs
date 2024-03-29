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
using HandsLiftedApp.Core.Importers;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PowerPointLib;
using NaturalSort.Extension;

namespace HandsLiftedApp.Core
{
    internal class CreateItem
    {
        public static readonly string[] SUPPORTED_SONG = { "txt", "xml" };
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        public static readonly string[] SUPPORTED_VIDEO = { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };
        public static readonly string[] SUPPORTED_IMAGE = { "bmp", "png", "jpg", "jpeg" };
        public static readonly string[] SUPPORTED_PDF= { "pdf" };
        
        public static async Task OpenPresentationFileAsync(string filePath, PlaylistInstance currentPlaylist)
        {
            //try
            //{
            //var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

            //foreach (var path in filePaths)
            //{

            if (filePath != null && filePath is string)
            {

                DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(filePath);

                string targetDirectory = Path.Join(currentPlaylist
                    .PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                if (fileName.EndsWith(".pptx"))
                {
                    Log.Debug($"Importing PowerPoint file: {filePath}");
                    PowerPointSlidesGroupItem slidesGroup = new PowerPointSlidesGroupItem() { Title = fileName, SourcePresentationFile = filePath };
                    
                    Class1.Run(filePath);

                    Log.Debug($"Importing PDF file: {filePath}");
                    // PDFSlidesGroupItem slidesGroup = new PDFSlidesGroupItem() { Title = fileName, SourcePresentationFile = filePath };

                    ConvertPDF.Convert(filePath + ".pdf", targetDirectory); // todo move into State as a SyncCommand
                    
                    MediaGroupItemInstance mediaGroupItem = new MediaGroupItemInstance(currentPlaylist)
                        { Title = "New media group" };
 
                    foreach (var convertedFilePath in Directory.GetFiles(targetDirectory).OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
                    {
                        mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                            { SourceMediaFilePath = convertedFilePath }); 
                    }

                    currentPlaylist.Items.Add(mediaGroupItem);
                    mediaGroupItem.GenerateSlides();
                }
                else if (filePath.EndsWith(".pdf"))
                {
                    Log.Debug($"Importing PDF file: {filePath}");
                    // PDFSlidesGroupItem slidesGroup = new PDFSlidesGroupItem() { Title = fileName, SourcePresentationFile = filePath };

                    ConvertPDF.Convert(filePath, targetDirectory); // todo move into State as a SyncCommand
                    
                    MediaGroupItemInstance mediaGroupItem = new MediaGroupItemInstance(currentPlaylist)
                        { Title = "New media group" };
 
                    foreach (var convertedFilePath in Directory.GetFiles(targetDirectory).OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
                    {
                        mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                            { SourceMediaFilePath = convertedFilePath }); 
                    }

                    currentPlaylist.Items.Add(mediaGroupItem);
                    mediaGroupItem.GenerateSlides();
                }
                else
                {
                    Log.Error($"Unsupported file type: {filePath}");
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

            return new ImageSlideInstance(filePath); // { Index = index };
        }
    }
}
