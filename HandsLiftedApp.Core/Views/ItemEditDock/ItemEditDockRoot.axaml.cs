using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.Views;
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

        private async void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            switch (DataContext)
            {
                case SongItemInstance item:
                {
                    SongEditorViewModel songEditorViewModel =
                        new SongEditorViewModel(item, Globals.Instance.MainViewModel.Playlist);
                    SongEditorWindow songEditorWindow = new SongEditorWindow() { DataContext = songEditorViewModel };
                    songEditorWindow.Show();
                    return;
                }
                case MediaGroupItemInstance mediaGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = mediaGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = pdfSlidesGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case PowerPointPresentationItemInstance powerPointPresentationItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = powerPointPresentationItemInstance };
                    editorWindow.Show();
                    return;
                }
                case GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = googleSlidesGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case ScriptureItemInstance scripture:
                {
                    var parentWindow = TopLevel.GetTopLevel(this) as Window;
                    if (parentWindow == null) return;

                    var dialog = new ScriptureAddDialog(scripture.Book, scripture.StartChapter, scripture.StartVerse,
                        scripture.EndChapter, scripture.EndVerse);
                    await dialog.ShowDialog(parentWindow);
                    if (dialog.Result == null) return;

                    var result = dialog.Result.Value;
                    scripture.Book = result.BookCode;
                    scripture.StartChapter = result.StartChapter;
                    scripture.StartVerse = result.StartVerse;
                    scripture.EndChapter = result.EndChapter;
                    scripture.EndVerse = result.EndVerse;
                    scripture.Title = ScriptureTitleFormatter.Format(result.BookName, result.StartChapter,
                        result.StartVerse, result.EndChapter, result.EndVerse);

                    // forceInvalidateCache: true — UpdatePages reuses existing ScriptureSlideInstances
                    // by page index and only resets a reused slide's Cached bitmap when the resolved
                    // theme object changed; an edited verse range (this call) can produce the same
                    // page count with entirely different text, which that reuse check alone would not
                    // catch, leaving a stale cached thumbnail.
                    _ = scripture.GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
                        t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", scripture.Title),
                        TaskContinuationOptions.OnlyOnFaulted);
                    return;
                }
            }
        }

        private void FadeOverrideCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { DataContext: Item item } checkBox)
            {
                return;
            }

            if (checkBox.IsChecked == true)
            {
                if (item.SlideTransitionDurationMs is null)
                {
                    item.SlideTransitionDurationMs = Globals.Instance.MainViewModel.Playlist.SlideTransitionDurationMs;
                }
            }
            else
            {
                item.SlideTransitionDurationMs = null;
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

                    instance.SourcePresentationFile = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(
                        filePaths[0].Path.LocalPath,
                        instance.ParentPlaylist.PlaylistWorkingDirectory);

                    instance.Sync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error changing PowerPoint file");
                }
            }
        }
    }
}