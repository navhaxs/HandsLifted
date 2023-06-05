using Avalonia.Threading;
using Avalonia.Controls;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static HandsLiftedApp.Importer.PowerPoint.Main;
using HandsLiftedApp.Models.UI;

namespace HandsLiftedApp.Models.ItemExtensionState
{
    public class PowerPointSlidesGroupItemStateImpl : ReactiveObject, IPowerPointSlidesGroupItemState
    {
        PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> parentSlidesGroup;

        // required for Activator
        public PowerPointSlidesGroupItemStateImpl(ref PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> parent)
        {
            parentSlidesGroup = parent;

            ChangeFileCommand = ReactiveCommand.CreateFromTask(ChangeFileCommandTask);
            ExploreFile = ReactiveCommand.Create(ExploreFileTask);
            EditInExternalCommand = ReactiveCommand.Create(EditInExternalCommandTask);
        }

        private bool _isSyncBusy = true;
        public bool IsSyncBusy { get => _isSyncBusy; set => this.RaiseAndSetIfChanged(ref _isSyncBusy, value); }

        public double _progress = 0;
        public double Progress { get => _progress; set => this.RaiseAndSetIfChanged(ref _progress, value); }

        public ReactiveCommand<Unit, Unit> ChangeFileCommand { get; }
        public ReactiveCommand<Unit, Unit> ExploreFile { get; }
        public ReactiveCommand<Unit, Unit> EditInExternalCommand { get; }
        //SourcePresentationFileExists
        //SourceSlidesExportDirectoryExists

        private async Task ChangeFileCommandTask()
        {
            // *gives up on MVVM*
            MessageBus.Current.SendMessage(new WrapFileOpenActionMessage()
            {
                CallbackAction = (fileName) =>
                {
                    if (fileName != null)
                    {
                        parentSlidesGroup.SourcePresentationFile = fileName;
                        // TODO DRY
                        //string PlaylistWorkingDirectory = @"C:\VisionScreensTmp\"; // i need a reference to Playlist :(
                        //DateTime now = DateTime.Now;
                        //string targetDirectory = Path.Join(PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                        //Directory.CreateDirectory(targetDirectory);
                        parentSlidesGroup.SyncState.SyncCommand();
                    }
                }
            });
        }

        private void ExploreFileTask()
        {
            FileUtils.ExploreFile(parentSlidesGroup.SourcePresentationFile);
        }
        
        private void EditInExternalCommandTask()
        {
            if (File.Exists(parentSlidesGroup.SourcePresentationFile))
            {
                using Process fileopener = new Process();
                fileopener.StartInfo.FileName = "explorer";
                fileopener.StartInfo.Arguments = "\"" + parentSlidesGroup.SourcePresentationFile + "\"";
                fileopener.Start();
            }
            else
            {
                // file does NOT exist
            }
        }

        public void SyncCommand()
        {
            DateTime now = DateTime.Now;
            string fileName = Path.GetFileName(parentSlidesGroup.SourcePresentationFile);

            // decision: where to import? do we import the source file? or just the exported data?
            // OR relative to PLAYLIST directory
            string targetDirectory = Path.Join(Globals.Env.TempDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
            //string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));

            ImportTask importTask = new ImportTask() { PPTXFilePath = parentSlidesGroup.SourcePresentationFile, OutputDirectory = targetDirectory };
            PlaylistUtils.AddPowerPointToPlaylist(importTask, (e) =>
            {
                Progress = e.JobPercentage;
            }).ContinueWith((s) =>
            {
                PlaylistUtils.UpdateSlidesGroup(ref parentSlidesGroup, targetDirectory);
                IsSyncBusy = false;

                //// TODO cleanup: delete old dir after completion
                var old = parentSlidesGroup.SourceSlidesExportDirectory;
                parentSlidesGroup.SourceSlidesExportDirectory = targetDirectory;

                FileUtils.DeleteDirectory(old);
            });
        }
    }
}
