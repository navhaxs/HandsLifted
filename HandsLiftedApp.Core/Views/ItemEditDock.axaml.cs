using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.Views
{
    public partial class ItemEditDock : UserControl
    {
        public ItemEditDock()
        {
            InitializeComponent();
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

                    instance.SourcePresentationFile = filePaths[0].Path.LocalPath;
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message);
                }
            }
        }
    }
}