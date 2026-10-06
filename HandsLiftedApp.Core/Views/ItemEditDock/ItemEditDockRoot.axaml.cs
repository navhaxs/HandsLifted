using System;
using System.Diagnostics;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Data.Models.Items;
using Serilog;

namespace HandsLiftedApp.Core.Views.ItemEditDock
{
    public partial class ItemEditDockRoot : UserControl
    {
        public ItemEditDockRoot()
        {
            InitializeComponent();
        }

        private void MotionVideoPicker_OnVideoPathPicked(object? sender, string path)
        {
            if (DataContext is SongItemInstance song)
                song.MotionBackgroundVideoOverride = path;
        }

        private void ClearThemeButton_OnClick(object? sender, RoutedEventArgs e)
        {
            switch (DataContext)
            {
                case SongItemInstance song:
                    song.ResolvedDesignTheme = null;
                    break;
                case ScriptureItemInstance scripture:
                    scripture.ExplicitDesignTheme = null;
                    break;
            }
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is IItemSyncable instance)
            {
                instance.Sync();
            }
        }

        private void PowerPoint_EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is PowerPointPresentationItemInstance instance)
            {
                Process.Start(new ProcessStartInfo("POWERPNT.exe", $"\"{instance.SourcePresentationFile}\"")
                    { UseShellExecute = true });
            }
        }

        private async void PowerPoint_ChangeFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is PowerPointPresentationItemInstance instance)
            {
                try
                {
                    var filePaths =
                        await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(new FilePickerOpenOptions() { SuggestedStartLocation = TopLevel.GetTopLevel(this).StorageProvider.TryGetFolderFromPathAsync(instance.SourcePresentationFile).Result });
                    if (filePaths == null || filePaths.Count == 0) return;

                    instance.SourcePresentationFile = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(
                        filePaths[0].Path.LocalPath,
                        Globals.Instance.AppPreferences?.MediaLibraryPath);

                    instance.Sync();
                }
                catch (MediaLibraryNotConfiguredException ex)
                {
                    Log.Warning(ex, "Cannot change PowerPoint file: Media Library not configured");
                    GoogleSlidesReauthWindow.ShowError("Media Library Not Configured", ex.Message);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error changing PowerPoint file");
                }
            }
        }
    }
}
