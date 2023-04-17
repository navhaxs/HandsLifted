using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using static HandsLiftedApp.Importer.GoogleSlides.Main;

namespace HandsLiftedApp.Models.ItemExtensionState
{
    public class GoogleSlidesGroupItemStateImpl : ReactiveObject, IGoogleSlidesGroupItemState
    {
        GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl> parentSlidesGroup;

        // required for Activator
        public GoogleSlidesGroupItemStateImpl(ref GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl> parent)
        {
            parentSlidesGroup = parent;

        }

        private bool _isSyncBusy = true;

        public bool IsProgressIndeterminate { get => _isSyncBusy; set => this.RaiseAndSetIfChanged(ref _isSyncBusy, value); }

        public double _progress = 0;


        public double Progress { get => _progress; set => this.RaiseAndSetIfChanged(ref _progress, value); }

        public void EditInExternalCommand()
        {
            //#slide=id.g150f86ebac7_0_0
            UrlUtils.OpenUrl($"https://docs.google.com/presentation/d/{parentSlidesGroup.SourceGooglePresentationId}/edit");
        }

        public void SyncCommand()
        {
            DateTime now = DateTime.Now;
            string fileName = parentSlidesGroup.SourceGooglePresentationId;

            string targetDirectory = Path.Join(@"R:\" + FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
            //string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
            IsProgressIndeterminate = true;
            ImportTask importTask = new ImportTask() { GoogleSlidesPresentationId = parentSlidesGroup.SourceGooglePresentationId, OutputDirectory = targetDirectory };
            PlaylistUtils.AddGoogleSlidesToPlaylist(importTask, (e) =>
            {
                //Progress = e.JobPercentage;
            }).ContinueWith((s) =>
            {
                IsProgressIndeterminate = false;

                if (s.Result == null)
                {
                    Log.Error("Google Slides import fail!");
                    // log error message
                    // show error message
                }
                else
                {
                    parentSlidesGroup.Title = s.Result.Title;

                    ConvertPDF.Convert(s.Result.OutputFullFilePath, targetDirectory, (progress) => Progress = progress);

                    PlaylistUtils.UpdateSlidesGroup(ref parentSlidesGroup, targetDirectory);

                    //// TODO cleanup: delete old dir after completion
                    var old = parentSlidesGroup.SourceSlidesExportDirectory;
                    parentSlidesGroup.SourceSlidesExportDirectory = targetDirectory;

                    FileUtils.DeleteDirectory(old);
                }

            });
        }
    }
}
