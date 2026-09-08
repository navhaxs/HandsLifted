using System.IO;
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
                return new LogoItemInstance(playlist) { Title = i.Title, SlideTransitionDurationMs = i.SlideTransitionDurationMs };
            }
            else if (deserializedItem is SongItemReference songReference)
            {
                var instance = new SongItemInstance(playlist)
                {
                    UUID = songReference.UUID,
                    SongId = songReference.SongId,
                    SlideTransitionDurationMs = songReference.SlideTransitionDurationMs
                };
                instance.RaiseForwardedPropertiesChanged();
                return instance;
            }
            else if (deserializedItem is SongItem songItem)
            {
                // Either a freshly-parsed library file (add-from-library flow — CreateItem.GenerateItem
                // just deserialized it) or legacy inline playlist content (old-format playlist file,
                // pre-dating SongItemReference). Both cases: if this UUID isn't already known to the
                // shared index, this object is the best available content for it — import it so the
                // reference resolves. If it's already known (the common add-from-library case, since
                // SongLibrary's scan already registered it), leave the existing cached entry alone
                // rather than overwriting it with what may be a stale re-parse.
                if (Globals.Instance.SongLibraryIndex.Resolve(songItem.UUID) == null)
                {
                    var libraryDirectory = playlistDirectoryPath ?? Path.GetTempPath();
                    Globals.Instance.SongLibraryIndex.ImportAndCache(songItem, libraryDirectory);
                }

                // A fresh playlist item — gets its own Item-constructor-assigned UUID (playlist-item
                // identity), distinct from SongId (which song it points at). Do NOT copy songItem.UUID
                // onto this instance's UUID: that would make this playlist item's own identity equal to
                // the song's identity, reintroducing UUID/SongId ambiguity for slide navigation.
                var instance = new SongItemInstance(playlist)
                {
                    SongId = songItem.UUID,
                    SlideTransitionDurationMs = songItem.SlideTransitionDurationMs
                };
                instance.RaiseForwardedPropertiesChanged();
                return instance;
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
                    EndVerse = scriptureItem.EndVerse,
                    Design = scriptureItem.Design,
                    SlideTransitionDurationMs = scriptureItem.SlideTransitionDurationMs
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
                        powerPointPresentationItem.SourceSlidesExportDirectory),
                    SlideTransitionDurationMs = powerPointPresentationItem.SlideTransitionDurationMs
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
                        pdfSlidesGroupItem.SourceSlidesExportDirectory),
                    SlideTransitionDurationMs = pdfSlidesGroupItem.SlideTransitionDurationMs
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
                        googleSlidesGroupItem.SourceSlidesExportDirectory),
                    SlideTransitionDurationMs = googleSlidesGroupItem.SlideTransitionDurationMs
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
                    AutoAdvanceTimer = mediaGroupItem.AutoAdvanceTimer,
                    SlideTransitionDurationMs = mediaGroupItem.SlideTransitionDurationMs
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