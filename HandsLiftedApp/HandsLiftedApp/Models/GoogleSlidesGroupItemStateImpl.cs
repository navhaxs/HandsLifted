using Avalonia.Threading;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using static HandsLiftedApp.Importer.GoogleSlides.Main;

namespace HandsLiftedApp.Models
{
    public class GoogleSlidesGroupItemStateImpl : ReactiveObject, IGoogleSlidesGroupItemState
    {
        GoogleSlidesGroupItem<ItemStateImpl, GoogleSlidesGroupItemStateImpl> parentSlidesGroup;

        // required for Activator
        public GoogleSlidesGroupItemStateImpl(ref GoogleSlidesGroupItem<ItemStateImpl, GoogleSlidesGroupItemStateImpl> parent)
        {
            parentSlidesGroup = parent;


            timer.Tick += (sender, e) =>
            {
                if (!parentSlidesGroup.State.IsSelected)
                    return;

                parentSlidesGroup.State.SelectedIndex = (parentSlidesGroup.State.SelectedIndex + 1) % parentSlidesGroup.Slides.Count;
            };
            timer.Start();

            //parentSlidesGroup.WhenAnyValue(s => s.State.SelectedIndex, (int s) =>
            //{
            //    timer.Stop();
            //    timer.Start();
            //    return s;
            //});
            //parentSlidesGroup.State.PageTransition = new XTransitioningContentControl.XFade(TimeSpan.FromSeconds(2.300));

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
            PlaylistUtils.AddGoogleSlidesToPlaylist(importTask, (ImportStats e) =>
            {
                //Progress = e.JobPercentage;
            }).ContinueWith((s) =>
            {
                IsProgressIndeterminate = false;

                parentSlidesGroup.Title = s.Result.Title;

                ConvertPDF.Convert(s.Result.OutputFullFilePath, targetDirectory, (progress) => Progress = progress);

                PlaylistUtils.UpdateSlidesGroup(ref parentSlidesGroup, targetDirectory);

                //// TODO cleanup: delete old dir after completion
                var old = parentSlidesGroup.SourceSlidesExportDirectory;
                parentSlidesGroup.SourceSlidesExportDirectory = targetDirectory;

                if (old != null)
                {
                    Directory.Delete(old, true);
                }
            });
        }


        // how to make this generic?
        DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
    }
}
