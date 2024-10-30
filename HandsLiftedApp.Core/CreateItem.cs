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
using System.Xml.Serialization;
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
        public static Item? OpenPresentationFile(string filePath, PlaylistInstance currentPlaylist)
        {
            Item? returnValue = CreatePresentationItem(filePath);

            if (returnValue != null)
            {
                return ItemInstanceFactory.ToItemInstance(returnValue, currentPlaylist);
            }
            // if (returnValue == null)
            
            // Dispatcher.UIThread.InvokeAsync(() =>
            // {
            //     // wait for UI to update...
            //     Dispatcher.UIThread.RunJobs();
            //     // and now we can jump to view
            //     var count = currentPlaylist.Items.Count;
            //     MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
            // });

            return returnValue;
        }

        public static Item? CreatePresentationItem(string filePath)
        {
            if (filePath != null && filePath is string)
            {
                // DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(filePath);

                // string targetDirectory = Path.Join(currentPlaylist
                //         .PlaylistWorkingDirectory,
                //     FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                // Directory.CreateDirectory(targetDirectory);

                if (fileName.EndsWith(".pptx") || fileName.EndsWith(".ppt"))
                {
                    PowerPointPresentationItem slidesGroup =
                        new PowerPointPresentationItem()
                            { Title = fileName, SourcePresentationFile = filePath };
                    // slidesGroup._Sync();
                    return slidesGroup;
                }
                else if (filePath.EndsWith(".pdf"))
                {
                    PDFSlidesGroupItem mediaGroupItem = new PDFSlidesGroupItem()
                        { Title = fileName, SourcePresentationFile = filePath };

                    // foreach (var convertedFilePath in Directory.GetFiles(targetDirectory)
                    //              .OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
                    // {
                    //     mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                    //         { SourceMediaFilePath = convertedFilePath });
                    // }

                    // mediaGroupItem.GenerateSlides();
                    return mediaGroupItem;
                }
                Log.Error($"Unsupported file type: {filePath}");
            }

            return null;
        }

        public static MediaSlide GenerateMediaContentSlide(MediaGroupItem.MediaItem mediaItem,
            MediaGroupItem parentMediaGroupItem)
        {
            string fullFilePath = mediaItem.SourceMediaFilePath;
            string filename = fullFilePath.ToLower();

            if (filename.EndsWith(".axaml"))
            {
                return new CustomAxamlSlideInstance(mediaItem);
            }
            else if (Constants.SUPPORTED_VIDEO.Any(x => filename.EndsWith(x)))
            {
                return new VideoSlideInstance(fullFilePath);
            }
            else
            {
                return new ImageSlideInstance(fullFilePath, parentMediaGroupItem);
            }
        }

        public static Item? GenerateItem(string filePath)
        {
            // TODO important - refactor to:
            // 1. Create plain Item object
            // 2. (Callee) Convert plain Item object to "ItemInstance" - refactor this code from HandsLiftedDocXmlSerializer
            string filename = filePath.ToLower();

            if (filePath.ToLower().EndsWith(".xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SongItem));
                using (FileStream stream = new FileStream(filePath, FileMode.Open))
                {
                    return (SongItemInstance)serializer.Deserialize(stream);
                }
            }
            else if (filePath.ToLower().EndsWith(".txt"))
            {
                return SongImporter.createSongItemFromTxtFile(filePath);
            }
            else if (Constants.SUPPORTED_VIDEO.Any(x => filename.EndsWith(x)) ||
                     Constants.SUPPORTED_IMAGE.Any(x => filename.EndsWith(x)))
            {
                var mediaGroupItem = new MediaGroupItem()
                    { Title = "New media group" };

                // foreach (var filePath in filePaths)
                // {
                //     if (filePath != null && filePath is string)
                //     {
                // DateTime now = DateTime.Now;
                // string fileName = Path.GetFileName(filePath);
                // string folderName = Path.GetDirectoryName(filePath);
                mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                    { SourceMediaFilePath = filePath });
                //     }
                // }
                // mediaGroupItem.GenerateSlides();

                return mediaGroupItem;
                // return SongImporter.createSongItemFromTxtFile(filePath);
            }
            else if (Constants.SUPPORTED_PDF.Any(x => filename.EndsWith(x)) || Constants.SUPPORTED_POWERPOINT.Any(x => filename.EndsWith(x)))
            {
                return CreatePresentationItem(filename);
            }

            return null;
        }
    }
}