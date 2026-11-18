using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Services.GoogleSignIn;
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
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
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using HandsLiftedApp.Importer.GoogleSlides;
using HandsLiftedApp.Importer.PDF;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class GoogleSlidesGroupItemInstance : GoogleSlidesGroupItem, IItemInstance, IItemDirtyBit, IItemSyncable, ISlideHoldable, IGoogleSyncWaiter
    {
        public PlaylistInstance ParentPlaylist { get; set; }

        public event EventHandler ItemDataModified;

        private bool _IsBusy = false;

        public bool IsBusy
        {
            get => _IsBusy;
            set => this.RaiseAndSetIfChanged(ref _IsBusy, value);
        }

        private double _ImportProgress = -1;

        public double ImportProgress
        {
            get => _ImportProgress;
            set => this.RaiseAndSetIfChanged(ref _ImportProgress, value);
        }

        private DateTime? _lastSyncDateTime = null;

        public DateTime? LastSyncDateTime
        {
            get => _lastSyncDateTime;
            set => this.RaiseAndSetIfChanged(ref _lastSyncDateTime, value);
        }

        private BlankSlide _blankSlide = new();

        private static readonly object syncSlidesLock = new object();

        // Set when a sign-in that this item waited on succeeded, so the retried Sync() does not prompt again.
        private volatile bool _resumedAfterSignIn;

        // SuccessGeneration for which the "new sign-in still rejected" dialog was already shown; -1 = never.
        private static int _lastRejectionDialogGeneration = -1;

        public GoogleSlidesGroupItemInstance(PlaylistInstance parentPlaylist)
        {
            ParentPlaylist = parentPlaylist;
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides, x => x.HeldSlide, (selectedSlideIndex, slides, held) =>
                {
                    // a slide held across a Sync stays live until the user navigates
                    if (held != null) return held;

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
                i => i.SourceGooglePresentationId,
                i => i.SourceSlidesExportDirectory
            ).Subscribe(_ => { ItemDataModified?.Invoke(this, EventArgs.Empty); });

            // in-place edits (e.g. the auto-advance timer) that WhenAnyValue's reference de-dupe can't see
            ContentChanged += (_, _) => ItemDataModified?.Invoke(this, EventArgs.Empty);
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

        private Slide? _heldSlide;

        public Slide? HeldSlide
        {
            get => _heldSlide;
            set => this.RaiseAndSetIfChanged(ref _heldSlide, value);
        }

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

        // keys of the current slides (see SlideIdentity), so the next Sync can tell which slide is which
        private List<string?>? _slideKeys;

        public void Sync()
        {
            if (IsBusy)
            {
                return;
            }

            // Captured when the work is queued, not when it runs: a sync that waited behind slow imports and
            // reaches an expired token after the user cancelled a sign-in must not prompt again.
            var cancelEpochAtQueue = GoogleSignInCoordinator.Instance.CancelEpoch;

            IsBusy = true;

            ImportWorkerThread.priorityQueue.Add(new ImportWorkerThread.BackgroundWorkRequest()
            {
                Callback = () =>
                {
                    // Read and clear: only the sync that follows a successful sign-in may not ask for another one.
                    var resumedAfterSignIn = _resumedAfterSignIn;
                    _resumedAfterSignIn = false;
                    var handedOff = false;

                    lock (syncSlidesLock)
                    {
                        try
                        {
                            string targetDirectory = ImportCacheService.GetKeyedCacheDirectory(
                                "GoogleSlidesPresentationId", SourceGooglePresentationId);

                            Log.Debug($"Importing Google Slides Presentation: {SourceGooglePresentationId}");

                            var importStats = Main.RunGoogleSlidesImportTask(null,
                                new Main.GoogleSlidesPresentationImporter()
                                {
                                    GoogleSlidesPresentationId = SourceGooglePresentationId,
                                    OutputDirectory = targetDirectory,
                                },
                                Globals.Instance.AppPreferences.GoogleClientId,
                                Globals.Instance.AppPreferences.GoogleClientSecret
                            );

                            // the title coming back from the import is not a user edit: keep it out of undo history
                            using (ParentPlaylist?.PropertyRecorder.Suppress())
                            {
                                this.Title = importStats.Title;
                            }

                            // The cache directory is keyed by presentation id, which says nothing about content. Export
                            // the downloaded PDF into a directory named after *its* content: an edited deck gets fresh
                            // pages at fresh paths (no leftovers, no stale cached images), an unchanged one is reused.
                            string exportDirectory = ImportCacheService.GetContentSubdirectory(
                                targetDirectory, importStats.OutputFullFilePath, "_export_");

                            if (ImportCacheService.HasUsableCachedExports(exportDirectory))
                            {
                                Log.Debug("Reusing cached Google Slides pages from {ExportDirectory}", exportDirectory);
                            }
                            else
                            {
                                ImportCacheService.BeginImport(exportDirectory);
                                Log.Debug($"Importing PDF file: {importStats.OutputFullFilePath}");
                                var converted = PdfRasterizer.Convert(new ImportTask()
                                {
                                    InputFile = importStats.OutputFullFilePath,
                                    OutputDirectory = exportDirectory,
                                    ExportFileFormat = ImportTask.ExportFileFormatType.PNG
                                });
                                if (!converted)
                                    throw new InvalidOperationException("Google Slides page conversion did not complete");
                                ImportCacheService.MarkImportComplete(exportDirectory);
                            }

                            var oldKeys = _slideKeys;
                            var oldIndex = SelectedSlideIndex;
                            SyncSlideHold.Begin(this, ParentPlaylist);
                            var newItems = new TrulyObservableCollection<GroupItem>();
                            var files = new List<string>();
                            foreach (var convertedFilePath in Directory.GetFiles(exportDirectory)
                                         .Where(f => !Path.GetFileName(f).StartsWith("."))
                                         .OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
                            {
                                newItems.Add(new MediaItem()
                                    { SourceMediaFilePath = convertedFilePath });
                                files.Add(convertedFilePath);
                            }

                            // Google object ids when they line up with the exported pages, else page images
                            var newKeys = SlideIdentity.KeysFor(files, importStats.SlideObjectIds);
                            _slideKeys = newKeys;

                            Items = newItems;

                            Log.Debug($"Generating slides");
                            GenerateSlides();

                            SyncSlideHold.Follow(this, oldKeys, oldIndex, newKeys);

                            Log.Debug($"Import OK");

                            LastSyncDateTime = DateTime.Now;
                        }
                        catch (TokenExpiredImportException)
                        {
                            if (resumedAfterSignIn)
                            {
                                // A fresh sign-in was just accepted yet Google still rejects the token: asking again
                                // would loop forever, so tell the user and release the item as for any other failure.
                                // Every item resumed by one sign-in fails the same way: show the dialog once per sign-in.
                                Log.Warning("Google Slides token still rejected right after a successful sign-in");
                                var generation = GoogleSignInCoordinator.Instance.SuccessGeneration;
                                if (Interlocked.Exchange(ref _lastRejectionDialogGeneration, generation) != generation)
                                {
                                    GoogleSlidesReauthWindow.ShowError("Google Sign-In Failed",
                                        "Google did not accept the new sign-in. Try signing in again from Setup.");
                                }
                            }
                            else if (GoogleSignInCoordinator.Instance.CancelEpoch != cancelEpochAtQueue)
                            {
                                // The user cancelled a sign-in after this sync was queued. Respect that: no prompt,
                                // no dialog. The item ends normally (handedOff stays false, so IsBusy clears below);
                                // a sync the user starts now captures the new epoch and prompts as usual.
                                Log.Information("Google Slides token expired but sign-in was cancelled since this sync was queued - not prompting again");
                            }
                            else
                            {
                                Log.Warning("Google Slides token expired - handing this sync to the sign-in coordinator");
                                // Hand off without blocking: the coordinator runs the sign-in on its own task and
                                // calls ResumeAfterSignIn / AbandonSignIn when it ends. The item stays busy until then.
                                // The owner window is a UI object, so it is read on the UI thread.
                                handedOff = true;
                                Dispatcher.UIThread.Post(() =>
                                {
                                    try
                                    {
                                        var mainWindow =
                                            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                                            ?.MainWindow;
                                        _ = GoogleSignInCoordinator.Instance.RequestSignIn(mainWindow, showExplanation: true,
                                            waiter: this);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Could not start Google sign-in for a Google Slides sync");
                                        AbandonSignIn();
                                    }
                                });
                            }
                        }
                        catch (ImportFailureException e)
                        {
                            Log.Error(e, "Error importing Google Slides presentation file");
                            Dispatcher.UIThread.Post(() =>
                            {
                                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                                new GoogleSlidesReauthWindow(
                                    "Google Slides Import Failed",
                                    string.IsNullOrWhiteSpace(e.Message) ? "Could not import this Google Slides presentation." : e.Message,
                                    isError: true).ShowDialog(mainWindow);
                            });
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error importing Google Slides presentation file");
                            Dispatcher.UIThread.Post(() =>
                            {
                                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                                new GoogleSlidesReauthWindow(
                                    "Google Slides Import Failed",
                                    "Could not import this Google Slides presentation. Check the log for details.",
                                    isError: true).ShowDialog(mainWindow);
                            });
                        }

                        // Left busy only when handed to the sign-in coordinator, which clears it via Resume/Abandon.
                        if (!handedOff) IsBusy = false;
                    }
                }
            });
        }

        public void ResumeAfterSignIn()
        {
            _resumedAfterSignIn = true;
            IsBusy = false; // Sync() returns early while busy
            Sync();
        }

        public void AbandonSignIn()
        {
            IsBusy = false;
        }
    }
}