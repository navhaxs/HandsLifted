using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PowerPointLib;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    /// <summary>
    /// A single video downloaded from a hosted video service (e.g. YouTube, Vimeo) via yt-dlp, added
    /// through Add Item > Online Video. Reuses the exact same download/quality/timeout/fail-soft
    /// behavior as PowerPoint's embedded-external-video import (<see cref="ExternalVideoDownloader"/>)
    /// by treating itself as a one-slide deck: the downloaded file always lands as "Slide.1.&lt;ext&gt;"
    /// in its own content-addressed cache directory (keyed by URL, not by file content, since there's
    /// no local file to hash).
    /// </summary>
    public class OnlineVideoItemInstance : OnlineVideoItem, IItemInstance, IItemDirtyBit, IItemSyncable
    {
        public PlaylistInstance ParentPlaylist { get; set; }

        public event EventHandler ItemDataModified;

        private bool _IsBusy = false;

        public bool IsBusy
        {
            get => _IsBusy;
            set => this.RaiseAndSetIfChanged(ref _IsBusy, value);
        }

        private string? _lastSyncWarning;

        public string? LastSyncWarning
        {
            get => _lastSyncWarning;
            set => this.RaiseAndSetIfChanged(ref _lastSyncWarning, value);
        }

        private DateTime? _lastSyncDateTime = null;

        public DateTime? LastSyncDateTime
        {
            get => _lastSyncDateTime;
            set => this.RaiseAndSetIfChanged(ref _lastSyncDateTime, value);
        }

        private BlankSlide _blankSlide = new();

        private static readonly object syncSlidesLock = new object();

        public OnlineVideoItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides, (selectedSlideIndex, slides) =>
                {
                    try
                    {
                        if (selectedSlideIndex > -1 && selectedSlideIndex < slides.Count)
                        {
                            return slides.ElementAt(selectedSlideIndex);
                        }
                    }
                    catch (Exception _ignored)
                    {
                    }

                    return _blankSlide;
                })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Items,
                i => i.Title,
                i => i.AutoAdvanceTimer,
                i => i.SourceVideoUrl
            ).Subscribe(_ => { ItemDataModified?.Invoke(this, EventArgs.Empty); });
        }

        public void GenerateSlides()
        {
            var x = new List<Slide>();
            foreach (var item in Items)
            {
                var generateMediaContentSlide = CreateItem.GenerateMediaContentSlide(item, this);
                x.Add(generateMediaContentSlide);
            }

            _Slides = x;

            this.RaisePropertyChanged(nameof(Slides));
        }

        public List<Slide> _Slides = new();
        public ObservableCollection<Slide> Slides => new(_Slides);

        public int _selectedSlideIndex = -1;

        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide?.Value;
        }

        public void Sync()
        {
            if (IsBusy) return;
            IsBusy = true;
            LastSyncWarning = null;

            ImportWorkerThread.priorityQueue.Add(new ImportWorkerThread.BackgroundWorkRequest()
            {
                Callback = () =>
                {
                    lock (syncSlidesLock)
                    {
                        try
                        {
                            string targetDirectory = ImportCacheService.GetKeyedCacheDirectory("OnlineVideoUrl", SourceVideoUrl);

                            var downloadedPath = FindDownloadedVideo(targetDirectory);
                            if (downloadedPath == null)
                            {
                                Log.Debug("Downloading online video: {Url}", SourceVideoUrl);
                                var warnings = new List<string>();

                                // ExternalVideoDownloader is built around PowerPoint's "one video per
                                // slide" model. Treating this item as a one-slide, one-video deck
                                // reuses its download/quality-cap/timeout/fail-soft logic exactly,
                                // with no changes to that already-hardened class.
                                ExternalVideoDownloader.DownloadPendingVideos(
                                    new[] { new PendingExternalVideo(1, SourceVideoUrl) },
                                    shownSlideCount: 1,
                                    targetDirectory,
                                    warnings);

                                if (warnings.Count > 0)
                                {
                                    LastSyncWarning = string.Join(Environment.NewLine, warnings);
                                }

                                downloadedPath = FindDownloadedVideo(targetDirectory);
                            }
                            else
                            {
                                Log.Debug("Online video already downloaded, skipping: {Url}", SourceVideoUrl);
                            }

                            if (downloadedPath != null)
                            {
                                var newItems = new TrulyObservableCollection<GroupItem>();
                                newItems.Add(new MediaItem { SourceMediaFilePath = downloadedPath });
                                Items = newItems;

                                Log.Debug("Generating slides");
                                GenerateSlides();
                            }

                            Log.Debug("Import OK");
                            LastSyncDateTime = DateTime.Now;
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, "Failed to import online video {Url}", SourceVideoUrl);
                            LastSyncWarning = "Couldn't import this video.";
                        }

                        IsBusy = false;
                    }
                }
            });
        }

        private static string? FindDownloadedVideo(string directory)
        {
            return Directory.GetFiles(directory)
                .FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), "Slide.1", StringComparison.OrdinalIgnoreCase) &&
                    Constants.SUPPORTED_VIDEO.Contains(
                        Path.GetExtension(f).TrimStart('.').ToLowerInvariant(), StringComparer.OrdinalIgnoreCase));
        }
    }
}
