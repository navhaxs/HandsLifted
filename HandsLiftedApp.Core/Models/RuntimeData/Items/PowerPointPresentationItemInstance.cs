using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PowerPointLib;
using HandsLiftedApp.Utils;
using NaturalSort.Extension;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class PowerPointPresentationItemInstance : PowerPointPresentationItem, IItemInstance, IItemDirtyBit, IItemSyncable
    {
        public PlaylistInstance ParentPlaylist { get; set; }

        public event EventHandler ItemDataModified;

        private bool _IsBusy = false;

        public bool IsBusy
        {
            get => _IsBusy;
            set => this.RaiseAndSetIfChanged(ref _IsBusy, value);
        }

        private DateTime? _lastSyncDateTime = null;

        public DateTime? LastSyncDateTime
        {
            get => _lastSyncDateTime;
            set => this.RaiseAndSetIfChanged(ref _lastSyncDateTime, value);
        }

        private BlankSlide _blankSlide = new();

        private static readonly object syncSlidesLock = new object();

        public PowerPointPresentationItemInstance(PlaylistInstance parentPlaylist)
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
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Items,
                i => i.Title,
                i => i.AutoAdvanceTimer,
                i => i.SourcePresentationFile,
                i => i.SourceSlidesExportDirectory
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

            ImportWorkerThread.priorityQueue.Add(new ImportWorkerThread.BackgroundWorkRequest()
            {
                Callback = () =>
                {
                    if (PowerPointImportSettings.UseNativeInterop && OperatingSystem.IsWindows())
                        SyncViaNativeInterop();
                    else
                        SyncViaSyncfusion();
                }
            });
        }

        private void SyncViaSyncfusion()
        {
            lock (syncSlidesLock)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string fileName = Path.GetFileName(SourcePresentationFile);

                    string targetDirectory = Path.Join(ParentPlaylist.PlaylistWorkingDirectory,
                        FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                        now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    Directory.CreateDirectory(targetDirectory);

                    Log.Debug($"Importing PowerPoint file (Syncfusion): {SourcePresentationFile}");
                    PresentationFileFormatConverter.Run(new ImportTask
                    {
                        InputFile = SourcePresentationFile,
                        OutputDirectory = targetDirectory,
                        ExportFileFormat = ImportTask.ExportFileFormatType.PDF
                    }, new ImportTaskReporter(stats => { }));

                    Log.Debug($"Converting PDF to slides: {SourcePresentationFile}");
                    ConvertPDF.Convert(new ImportTask
                    {
                        InputFile = SourcePresentationFile + ".pdf", // TODO: use output path from above step
                        OutputDirectory = targetDirectory,
                        ExportFileFormat = ImportTask.ExportFileFormatType.PDF
                    }, new ImportTaskReporter(stats => { }));

                    ApplySlidesFromDirectory(targetDirectory);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error importing PowerPoint file via Syncfusion");
                }
                IsBusy = false;
            }
        }

        [SupportedOSPlatform("windows")]
        private void SyncViaNativeInterop()
        {
            lock (syncSlidesLock)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string fileName = Path.GetFileName(SourcePresentationFile);

                    string targetDirectory = Path.Join(ParentPlaylist.PlaylistWorkingDirectory,
                        FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                        now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    Directory.CreateDirectory(targetDirectory);

                    Log.Debug($"Importing PowerPoint file (native COM): {SourcePresentationFile}");

                    var result = NativePowerPointImportService.RunImport(
                        new ImportTask
                        {
                            InputFile = SourcePresentationFile,
                            OutputDirectory = targetDirectory,
                            ExportFileFormat = ImportTask.ExportFileFormatType.PNG
                        },
                        new ImportTaskReporter(stats =>
                        {
                            Log.Debug("Native import progress: {Pct}% — {Status}",
                                stats.JobPercentage, stats.StatusMessage);
                        }));

                    if (result.JobStatus != ImportStats.JobStatusEnum.CompletionSuccess)
                    {
                        Log.Error("Native PowerPoint import failed with status {Status}", result.JobStatus);
                        IsBusy = false;
                        return;
                    }

                    ApplySlidesFromDirectory(targetDirectory);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error importing PowerPoint file via native interop");
                }
                IsBusy = false;
            }
        }

        private void ApplySlidesFromDirectory(string targetDirectory)
        {
            var newItems = new TrulyObservableCollection<GroupItem>();
            foreach (var convertedFilePath in Directory.GetFiles(targetDirectory)
                         .OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
            {
                // Skip non-image files (e.g. intermediate PDFs from Syncfusion path)
                var ext = Path.GetExtension(convertedFilePath).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                newItems.Add(new MediaItem { SourceMediaFilePath = convertedFilePath });
            }

            Items = newItems;

            Log.Debug("Generating slides");
            GenerateSlides();

            Log.Debug("Import OK");
            LastSyncDateTime = DateTime.Now;
        }
    }
}