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
using Serilog;

namespace HandsLiftedApp.Core
{
    public class HandsLiftedDocXmlSerializer
    {
        // Serialize Playlist to XML
        public static void SerializePlaylist(PlaylistInstance playlist, string? filePath)
        {
            byte[] bytes = SerializePlaylistToBytes(playlist, filePath);

            // only once all serialization is OK, now write to disk
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Serializes <paramref name="playlist"/> to the document XML without touching disk.
        /// <paramref name="filePath"/> is only used to relativize media paths against its directory.
        /// </summary>
        public static byte[] SerializePlaylistToBytes(PlaylistInstance playlist, string? filePath)
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
                SlideTransitionDurationMs = playlist.SlideTransitionDurationMs,
                DefaultSongThemeId = playlist.DefaultSongThemeId,
                DefaultSongMotionThemeId = playlist.DefaultSongMotionThemeId,
                DefaultScriptureThemeId = playlist.DefaultScriptureThemeId,
                Items = new TrulyObservableCollection<Item>()
            };

            // TODO: convert all 'instance/runtime' classes to 'serialized document' classes
            playlistSerialized.Items.AddRange(playlist.Items.ToList().ConvertAll(item =>
            {
                return SerializeItem(item);
                // return new Item
                // {
                //     Slides = item.Slides.ConvertAll(slide => new SlideSerialized()),
                // };
            }));

            using MemoryStream memoryStream = new MemoryStream();
            serializer.Serialize(memoryStream, playlistSerialized);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Relativizes <paramref name="path"/> against the configured Media Library folder, but
        /// only when it actually lives under that folder (or no library is configured at all). A
        /// source outside the library — an item added before the Media Library feature existed, or
        /// from an add path that predates it — would otherwise be written as
        /// <c>..\..\Users\someone\sermon.pdf</c>, which looks portable but is not.
        /// </summary>
        private static string? ToRelativePathIfUnderMediaLibrary(string? path)
        {
            if (path == null || !Path.IsPathFullyQualified(path))
            {
                return path;
            }

            var mediaLibraryPath = Globals.Instance.AppPreferences?.MediaLibraryPath;

            if (string.IsNullOrWhiteSpace(mediaLibraryPath) ||
                !RelativeFilePathResolver.IsUnderDirectory(mediaLibraryPath, path))
            {
                Log.Warning(
                    "Keeping absolute path for source outside the media library folder (not portable): {Path}",
                    path);
                return path;
            }

            return RelativeFilePathResolver.ToRelativePath(mediaLibraryPath, path);
        }

        // Copies a group's slides for the document, relativizing media paths against the media library.
        private static TrulyObservableCollection<MediaGroupItem.GroupItem> SerializeGroupItems(MediaGroupItem group)
        {
            return new TrulyObservableCollection<MediaGroupItem.GroupItem>(group.Items
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
                                ToRelativePathIfUnderMediaLibrary(mediaItem.SourceMediaFilePath);
                        }

                        return newMediaItem;
                    }

                    return item;
                }).ToList());
        }

        public static Item SerializeItem(Item item)
        {
            if (item is LogoItemInstance i)
            {
                return new LogoItem() { Title = i.Title, SlideTransitionDurationMs = i.SlideTransitionDurationMs };
            }
            else if (item is SongItemInstance songItemInstance)
            {
                // SlideTransitionDurationMs is a per-item override on the base Item class
                // (not song content) and, like every other branch here (LogoItem, ScriptureItem,
                // MediaGroupItem, ...), must be carried over explicitly. MotionBackgroundVideoOverride
                // is the same kind of per-item override, scoped to this playlist item rather than the
                // shared library song.
                return new SongItemReference
                {
                    UUID = songItemInstance.UUID,
                    SongId = songItemInstance.SongId,
                    SlideTransitionDurationMs = songItemInstance.SlideTransitionDurationMs,
                    MotionBackgroundVideoOverridePath = ToRelativePathIfUnderMediaLibrary(
                        songItemInstance.MotionBackgroundVideoOverride)
                };
            }
            else if (item is ScriptureItemInstance scriptureItemInstance)
            {
                return new ScriptureItem()
                {
                    UUID = scriptureItemInstance.UUID,
                    Title = scriptureItemInstance.Title,
                    Translation = scriptureItemInstance.Translation,
                    Book = scriptureItemInstance.Book,
                    StartChapter = scriptureItemInstance.StartChapter,
                    StartVerse = scriptureItemInstance.StartVerse,
                    EndChapter = scriptureItemInstance.EndChapter,
                    EndVerse = scriptureItemInstance.EndVerse,
                    Design = scriptureItemInstance.Design,
                    SlideTransitionDurationMs = scriptureItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is SlidesGroupItemInstance slidesGroupItemInstance)
            {
                return new SlidesGroupItem()
                {
                    UUID = slidesGroupItemInstance.UUID,
                    Title = slidesGroupItemInstance.Title,
                    SlideTransitionDurationMs = slidesGroupItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is MediaGroupItemInstance mediaGroupItemInstance)
            {
                return new MediaGroupItem()
                {
                    UUID = mediaGroupItemInstance.UUID,
                    Title = mediaGroupItemInstance.Title,
                    Items = SerializeGroupItems(mediaGroupItemInstance),
                    AutoAdvanceTimer = mediaGroupItemInstance.AutoAdvanceTimer,
                    SlideTransitionDurationMs = mediaGroupItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is OnlineVideoItemInstance onlineVideoItemInstance)
            {
                // The loader (ItemInstanceFactory) already rebuilds OnlineVideoItem from XML; without this
                // branch the instance fell through to `return item` and XmlSerializer threw
                // ("cannot be serialized because it does not have a parameterless constructor"), so a
                // playlist containing an online video could not be saved at all.
                return new OnlineVideoItem()
                {
                    UUID = onlineVideoItemInstance.UUID,
                    Title = onlineVideoItemInstance.Title,
                    Items = SerializeGroupItems(onlineVideoItemInstance),
                    AutoAdvanceTimer = onlineVideoItemInstance.AutoAdvanceTimer,
                    SourceVideoUrl = onlineVideoItemInstance.SourceVideoUrl,
                    SlideTransitionDurationMs = onlineVideoItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is PowerPointPresentationItemInstance powerPointPresentationItemInstance)
            {
                return new PowerPointPresentationItem()
                {
                    UUID = powerPointPresentationItemInstance.UUID,
                    Title = powerPointPresentationItemInstance.Title,
                    AutoAdvanceTimer = powerPointPresentationItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = ToRelativePathIfUnderMediaLibrary(
                        powerPointPresentationItemInstance.SourcePresentationFile),
                    SlideTransitionDurationMs = powerPointPresentationItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance)
            {
                return new GoogleSlidesGroupItem()
                {
                    UUID = googleSlidesGroupItemInstance.UUID,
                    Title = googleSlidesGroupItemInstance.Title,
                    AutoAdvanceTimer = googleSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourceGooglePresentationId = googleSlidesGroupItemInstance.SourceGooglePresentationId,
                    SlideTransitionDurationMs = googleSlidesGroupItemInstance.SlideTransitionDurationMs
                };
            }
            else if (item is PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance)
            {
                return new PDFSlidesGroupItem()
                {
                    UUID = pdfSlidesGroupItemInstance.UUID,
                    Title = pdfSlidesGroupItemInstance.Title,
                    AutoAdvanceTimer = pdfSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = ToRelativePathIfUnderMediaLibrary(
                        pdfSlidesGroupItemInstance.SourcePresentationFile),
                    SlideTransitionDurationMs = pdfSlidesGroupItemInstance.SlideTransitionDurationMs
                };
            }

            return item;
        }

        // Deserialize Playlist from XML
        public static Playlist DeserializePlaylist(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Playlist file not found: {filePath}", filePath);
            }

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
