using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class LogoEditorView : UserControl
    {
        public LogoEditorView()
        {
            InitializeComponent();

            playlistLogoPicker.GetObservable(TextBoxFilePathPicker.FilePathProperty).Subscribe(path =>
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                if (Globals.Instance.MainViewModel?.Playlist == null) return;

                var copiedPath = PortableAssetCopier.CopyIntoSubfolder(
                    path,
                    Globals.Instance.MainViewModel.Playlist.PlaylistWorkingDirectory,
                    Path.Combine("Themes", "Logo"));

                if (copiedPath != path)
                {
                    playlistLogoPicker.FilePath = copiedPath;
                }
            });
        }
    }
}
