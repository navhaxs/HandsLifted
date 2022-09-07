using Avalonia.Threading;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using static HandsLiftedApp.Importer.PowerPoint.Main;

namespace HandsLiftedApp.Models
{
    public class PowerPointSlidesGroupItemStateImpl : ReactiveObject, IPowerPointSlidesGroupItemState
    {
        PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl> parentSlidesGroup;

        // required for Activator
        public PowerPointSlidesGroupItemStateImpl(ref PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl> parent)
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

        public bool IsSyncBusy { get => _isSyncBusy; set => this.RaiseAndSetIfChanged(ref _isSyncBusy, value); }

        public double _progress = 0;


        public double Progress { get => _progress; set => this.RaiseAndSetIfChanged(ref _progress, value); }

        public void EditInPowerPointCommand()
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + parentSlidesGroup.SourcePresentationFile + "\"";
            fileopener.Start();
        }

        public void SyncCommand()
        {
            DateTime now = DateTime.Now;
            string fileName = Path.GetFileName(parentSlidesGroup.SourcePresentationFile);

            string targetDirectory = Path.Join(@"R:\" + FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
            //string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));

            ImportTask importTask = new ImportTask() { PPTXFilePath = parentSlidesGroup.SourcePresentationFile, OutputDirectory = targetDirectory };
            PlaylistUtils.AddPowerPointToPlaylist(importTask, (ImportStats e) =>
            {
                Progress = e.JobPercentage;
            }).ContinueWith((s) =>
            {
                PlaylistUtils.UpdateSlidesGroup(ref parentSlidesGroup, targetDirectory);
                IsSyncBusy = false;

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
