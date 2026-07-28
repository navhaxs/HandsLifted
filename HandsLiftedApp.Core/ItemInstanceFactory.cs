using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using Serilog;

namespace HandsLiftedApp.Core
{
    public static class ItemInstanceFactory
    {
        public static Item ToItemInstance(Item deserializedItem, PlaylistInstance? playlist)
        {
            var playlistDirectoryPath = playlist?.PlaylistWorkingDirectory;
            if (deserializedItem is LogoItem i)
            {
                return new LogoItemInstance(playlist) { Title = i.Title };
            }
            else if (deserializedItem is SongItem songItem)
            {
                var resolvedMotionBgPath = !string.IsNullOrEmpty(songItem.MotionBackgroundVideoPath)
                    ? RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath, songItem.MotionBackgroundVideoPath)
                    : null;

                Log.Debug("ItemInstanceFactory: SongItem '{Title}' MotionBg raw='{RawPath}', resolved='{ResolvedPath}', base='{BasePath}'",
                    songItem.Title, songItem.MotionBackgroundVideoPath, resolvedMotionBgPath, playlistDirectoryPath);

                var song = new SongItemInstance(playlist)
                {
                    UUID = songItem.UUID,
                    Title = songItem.Title,
                    Arrangement = songItem.Arrangement,
                    Arrangements = songItem.Arrangements,
                    SelectedArrangementId = songItem.SelectedArrangementId,
                    Stanzas = songItem.Stanzas,
                    Copyright = songItem.Copyright,
                    Design = songItem.Design,
                    StartOnTitleSlide = songItem.StartOnTitleSlide,
                    EndOnBlankSlide = songItem.EndOnBlankSlide,
                    MotionBackgroundVideoPath = resolvedMotionBgPath
                };
                // song.GenerateSlides();
                return song;
            }
            else if (deserializedItem is ScriptureItem scriptureItem)
            {
                var scripture = new ScriptureItemInstance(playlist)
                {
                    UUID = scriptureItem.UUID,
                    Title = scriptureItem.Title,
                    Translation = scriptureItem.Translation,
                    Book = scriptureItem.Book,
                    StartChapter = scriptureItem.StartChapter,
                    StartVerse = scriptureItem.StartVerse,
                    EndChapter = scriptureItem.EndChapter,
                    EndVerse = scriptureItem.EndVerse
                };
                // Fire-and-forget: GenerateSlidesAsync reads USX from local disk and
                // this factory method is synchronous. Slides populate reactively
                // once the read completes; ToItemInstance's other branches are
                // similarly inconsistent about invoking their own GenerateSlides
                // (e.g. SongItem's is commented out at the time of writing).
                _ = scripture.GenerateSlidesAsync().ContinueWith(
                    t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", scripture.Title),
                    TaskContinuationOptions.OnlyOnFaulted);
                return scripture;
            }
            else if (deserializedItem is PowerPointPresentationItem powerPointPresentationItem)
            {
                var g = new PowerPointPresentationItemInstance(playlist)
                {
                    UUID = powerPointPresentationItem.UUID,
                    Title = powerPointPresentationItem.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(powerPointPresentationItem.Items
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
                                        RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return newMediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = powerPointPresentationItem.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItem.SourcePresentationFile),
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItem.SourceSlidesExportDirectory)
                };
                g.GenerateSlides();
                return g;
            }
            else if (deserializedItem is PDFSlidesGroupItem pdfSlidesGroupItem)
            {
                var g = new PDFSlidesGroupItemInstance(playlist)
                {
                    UUID = pdfSlidesGroupItem.UUID,
                    Title = pdfSlidesGroupItem.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(pdfSlidesGroupItem.Items
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
                                        RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return newMediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = pdfSlidesGroupItem.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        pdfSlidesGroupItem.SourcePresentationFile),
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        pdfSlidesGroupItem.SourceSlidesExportDirectory)
                };
                g.GenerateSlides();
                return g;
            }
            else if (deserializedItem is GoogleSlidesGroupItem googleSlidesGroupItem)
            {
                var g = new GoogleSlidesGroupItemInstance(playlist)
                {
                    UUID = googleSlidesGroupItem.UUID,
                    Title = googleSlidesGroupItem.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(googleSlidesGroupItem.Items
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
                                        RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return newMediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = googleSlidesGroupItem.AutoAdvanceTimer,
                    SourceGooglePresentationId = googleSlidesGroupItem.SourceGooglePresentationId,
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        googleSlidesGroupItem.SourceSlidesExportDirectory)
                };
                g.GenerateSlides();
                return g;
            }
            else if (deserializedItem is MediaGroupItem mediaGroupItem)
            {
                var g = new MediaGroupItemInstance(playlist)
                {
                    UUID = mediaGroupItem.UUID,
                    Title = mediaGroupItem.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(mediaGroupItem.Items.Select(item =>
                    {
                        if (item is MediaGroupItem.MediaItem mediaItem)
                        {
                            // TODO deep copy
                            var newMediaItem = new MediaGroupItem.MediaItem()
                                { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                            if (newMediaItem.SourceMediaFilePath != null)
                            {
                                newMediaItem.SourceMediaFilePath =
                                    RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                        mediaItem.SourceMediaFilePath);
                            }

                            return newMediaItem;
                        }

                        return item;
                    }).ToList()),
                    AutoAdvanceTimer = mediaGroupItem.AutoAdvanceTimer
                };
                g.GenerateSlides();
                return g;
            }
            else
            {
                return deserializedItem;
            }
        }
    }
}