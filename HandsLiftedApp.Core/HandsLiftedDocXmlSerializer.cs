using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using DynamicData;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core
{
    public class HandsLiftedDocXmlSerializer
    {
        // Serialize Playlist to XML
        public static void SerializePlaylist(PlaylistInstance playlist, string? filePath)
        {
            string? playlistDirectoryPath = Path.GetDirectoryName(filePath);

            if (playlistDirectoryPath == null)
            {
                throw new Exception("Could not get directory path from file path.");
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Playlist));
            Playlist playlistSerialized = new Playlist
            {
                Title = playlist.Title,
                Meta = playlist.Meta,
                LogoGraphicFile =
                    RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath, playlist.LogoGraphicFile),
                Designs = new ObservableCollection<BaseSlideTheme>(playlist.Designs.Select(design =>
                {
                    if (design.BackgroundGraphicFilePath != null)
                    {
                        design.BackgroundGraphicFilePath =
                            RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                design.BackgroundGraphicFilePath);
                    }

                    return design;
                }).ToList()),
                Items = new TrulyObservableCollection<Item>()
            };

            // TODO: convert all 'instance/runtime' classes to 'serialized document' classes
            playlistSerialized.Items.AddRange(playlist.Items.ToList().ConvertAll(item =>
            {
                return SerializeItem(item, playlistDirectoryPath);
                // return new Item
                // {
                //     Slides = item.Slides.ConvertAll(slide => new SlideSerialized()),
                // };
            }));

            MemoryStream memoryStream = new MemoryStream();
            serializer.Serialize(memoryStream, playlistSerialized);

            // only once all serialization is OK, now write to disk
            memoryStream.Position = 0;
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                memoryStream.CopyTo(fileStream);
            }
        }

        public static Item SerializeItem(Item item, string playlistDirectoryPath)
        {
            if (item is LogoItemInstance i)
            {
                return new LogoItem() { Title = i.Title };
            }
            else if (item is SongItemInstance songItemInstance)
            {
                return new SongItem()
                {
                    UUID = songItemInstance.UUID,
                    Title = songItemInstance.Title,
                    Arrangement = songItemInstance.Arrangement,
                    Arrangements = songItemInstance.Arrangements,
                    SelectedArrangementId = songItemInstance.SelectedArrangementId,
                    Stanzas = songItemInstance.Stanzas,
                    Copyright = songItemInstance.Copyright,
                    Design = songItemInstance.Design,
                    StartOnTitleSlide = songItemInstance.StartOnTitleSlide,
                    EndOnBlankSlide = songItemInstance.EndOnBlankSlide
                };
            }
            else if (item is SlidesGroupItemInstance slidesGroupItemInstance)
            {
                return new SlidesGroupItem()
                {
                    UUID = slidesGroupItemInstance.UUID,
                    Title = slidesGroupItemInstance.Title,
                };
            }
            else if (item is MediaGroupItemInstance mediaGroupItemInstance)
            {
                return new MediaGroupItem()
                {
                    UUID = mediaGroupItemInstance.UUID,
                    Title = mediaGroupItemInstance.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(mediaGroupItemInstance.Items
                        .Select(item =>
                        {
                            if (item is MediaGroupItem.MediaItem mediaItem)
                            {
                                // TODO deep copy
                                var newMediaItem = new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                                if (newMediaItem.SourceMediaFilePath != null)
                                {
                                    newMediaItem.SourceMediaFilePath =
                                        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return mediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = mediaGroupItemInstance.AutoAdvanceTimer
                };
            }
            else if (item is PowerPointPresentationItemInstance powerPointPresentationItemInstance)
            {
                return new PowerPointPresentationItem()
                {
                    UUID = powerPointPresentationItemInstance.UUID,
                    Title = powerPointPresentationItemInstance.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(powerPointPresentationItemInstance
                        .Items
                        .Select(item =>
                        {
                            if (item is MediaGroupItem.MediaItem mediaItem)
                            {
                                // TODO deep copy
                                var newMediaItem = new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                                if (newMediaItem.SourceMediaFilePath != null)
                                {
                                    newMediaItem.SourceMediaFilePath =
                                        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return mediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = powerPointPresentationItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItemInstance.SourcePresentationFile),
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItemInstance.SourceSlidesExportDirectory)
                };
            }
            else if (item is GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance)
            {
                return new GoogleSlidesGroupItem()
                {
                    UUID = googleSlidesGroupItemInstance.UUID,
                    Title = googleSlidesGroupItemInstance.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(googleSlidesGroupItemInstance.Items
                        .Select(item =>
                        {
                            if (item is MediaGroupItem.MediaItem mediaItem)
                            {
                                // TODO deep copy
                                var newMediaItem = new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                                if (newMediaItem.SourceMediaFilePath != null)
                                {
                                    newMediaItem.SourceMediaFilePath =
                                        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return mediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = googleSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourceGooglePresentationId = googleSlidesGroupItemInstance.SourceGooglePresentationId,
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        googleSlidesGroupItemInstance.SourceSlidesExportDirectory)
                };
            }
            else if (item is PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance)
            {
                return new PDFSlidesGroupItem()
                {
                    UUID = pdfSlidesGroupItemInstance.UUID,
                    Title = pdfSlidesGroupItemInstance.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(pdfSlidesGroupItemInstance.Items
                        .Select(item =>
                        {
                            if (item is MediaGroupItem.MediaItem mediaItem)
                            {
                                // TODO deep copy
                                var newMediaItem = new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                                if (newMediaItem.SourceMediaFilePath != null)
                                {
                                    newMediaItem.SourceMediaFilePath =
                                        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return mediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = pdfSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        pdfSlidesGroupItemInstance.SourcePresentationFile),
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        pdfSlidesGroupItemInstance.SourceSlidesExportDirectory)
                };
            }

            return item;
        }

        // Deserialize Playlist from XML
        public static Playlist DeserializePlaylist(string filePath)
        {
            string? playlistDirectoryPath = Path.GetDirectoryName(filePath);

            if (playlistDirectoryPath == null)
            {
                throw new Exception("Could not get directory path from file path.");
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Playlist));

            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                var x = serializer.Deserialize(stream);
                if (x != null)
                {
                    Playlist deserialized = (Playlist)x;
                    return deserialized;
                }
            }

            throw new NotImplementedException();
        }
    }
}