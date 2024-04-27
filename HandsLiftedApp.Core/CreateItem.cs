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
        
        public static Item? OpenPresentationFile(string filePath, PlaylistInstance currentPlaylist)
        {
            Item? returnValue = null;

            if (filePath != null && filePath is string)
            {

                DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(filePath);

                string targetDirectory = Path.Join(currentPlaylist
                    .PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                if (fileName.EndsWith(".pptx") || fileName.EndsWith(".ppt"))
                {
                    PowerPointPresentationItemInstance slidesGroup = new PowerPointPresentationItemInstance(currentPlaylist) { Title = fileName, SourcePresentationFile = filePath };
                    slidesGroup._Sync();
                    returnValue = slidesGroup;
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

                    mediaGroupItem.GenerateSlides();
                    returnValue = mediaGroupItem;
                }
                else
                {
                    Log.Error($"Unsupported file type: {filePath}");
                    return null;
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
         
            return returnValue;
        }

        public static MediaSlide GenerateMediaContentSlide(MediaGroupItem.MediaItem mediaItem, MediaGroupItem parentMediaGroupItem)
        {
            string fullFilePath = mediaItem.SourceMediaFilePath;
            string filename = fullFilePath.ToLower();

            if (filename.EndsWith(".axaml"))
            {
                return new CustomAxamlSlideInstance(mediaItem);
            }
            else if (SUPPORTED_VIDEO.Any(x => filename.EndsWith(x)))
            {
                return new VideoSlideInstance(fullFilePath);
            }
            else
            {
                return new ImageSlideInstance(fullFilePath, parentMediaGroupItem);
            }
        }
    }
}
