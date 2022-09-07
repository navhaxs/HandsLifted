using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views.Editor;
using NetOffice.PowerPointApi;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using static HandsLiftedApp.Importer.PowerPoint.Main;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace HandsLiftedApp.Models
{
    public class PowerPointSlidesGroupItemStateImpl : ReactiveObject, IPowerPointSlidesGroupItemState
    {
        PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl> parentSlidesGroup;

        // required for Activator
        public PowerPointSlidesGroupItemStateImpl(ref PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl> parent)
        {
            parentSlidesGroup = parent;
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
                Directory.Delete(old, true);
            });
        }
    }
}
