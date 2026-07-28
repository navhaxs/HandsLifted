using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Core.Views.Editors;
using Serilog;

namespace HandsLiftedApp.Core.Views.LibraryView
{
    public partial class LibraryQueryView : UserControl
    {
        private const double DragThreshold = 4.0;
        private Point? _dragStart;
        private LibraryItem? _pendingDragItem;
        private Control? _pendingDragControl;

        public LibraryQueryView()
        {
            AsyncImageLoader.ImageLoader.AsyncImageLoader = ThumbnailEngineSettings.UseMpvEngine
                ? new MpvThumbnailImageLoader()
                : new WindowsThumbnailImageLoader();

            InitializeComponent();
        }

        private void DockPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is LibraryItem libraryItem)
            {
                _dragStart = e.GetPosition(null);
                _pendingDragItem = libraryItem;
                _pendingDragControl = control;
                control.PointerMoved += DockPanel_PointerMoved;
                control.PointerReleased += DockPanel_PointerReleased;
                e.Pointer.Capture(control);
            }
        }

        private void DockPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            CancelPendingDrag();
        }

        private void CancelPendingDrag()
        {
            if (_pendingDragControl != null)
            {
                _pendingDragControl.PointerMoved -= DockPanel_PointerMoved;
                _pendingDragControl.PointerReleased -= DockPanel_PointerReleased;
            }
            _dragStart = null;
            _pendingDragItem = null;
            _pendingDragControl = null;
        }

        private async void DockPanel_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStart == null || _pendingDragItem == null) return;

            var delta = e.GetPosition(null) - _dragStart.Value;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

            var item = _pendingDragItem;
            CancelPendingDrag();

            var dragData = new DataTransfer();
            var topLevel = TopLevel.GetTopLevel(this);
            IStorageFile file = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FullFilePath));
            dragData.Add(DataTransferItem.Create(DataFormat.File, file));

            await DragDrop.DoDragDropAsync(e, dragData, DragDropEffects.Copy);
        }

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
            }
        }
        
        private void AddItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryQueryViewModel vm) return;
            var songLibrary = vm.ActiveSongLibrary;
            if (songLibrary == null) return;

            var playlist = Globals.Instance.MainViewModel.Playlist;
            var editorVm = new SongEditorViewModel(new SongItemInstance(null), playlist)
            {
                SongLibrary = songLibrary
            };
            var editor = new SongEditorWindow { DataContext = editorVm };
            editor.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            editor.Show();
        }

        private SongLibrary? GetSongLibrary() =>
            (DataContext as LibraryQueryViewModel)?.ActiveSongLibrary;

        private static LibraryItem? GetClickedItem(object? sender)
        {
            return sender is Control { DataContext: LibraryItem item } ? item : null;
        }

        private ContextMenu? _emptySpaceMenu;

        private void SongListBox_ContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (e.Handled) return;

            // If source is inside a ListBoxItem, that item's ContextMenu handles it
            var source = e.Source as StyledElement;
            while (source != null && !ReferenceEquals(source, sender))
            {
                if (source is ListBoxItem) return;
                source = source.Parent;
            }

            if (_emptySpaceMenu == null)
            {
                var refresh = new MenuItem { Header = "Refresh Library" };
                refresh.Click += RefreshLibrary_OnClick;
                _emptySpaceMenu = new ContextMenu();
                _emptySpaceMenu.Items.Add(refresh);
            }
            _emptySpaceMenu.Open(sender as Control);
            e.Handled = true;
        }

        private void RefreshLibrary_OnClick(object? sender, RoutedEventArgs e)
        {
            GetSongLibrary()?.TriggerRefresh();
        }

        private async void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item == null || !File.Exists(item.FullFilePath)) return;
            var parent = TopLevel.GetTopLevel(this) as Window;
            if (parent == null) return;

            var dialog = new DeleteConfirmationWindow(item.Title);
            await dialog.ShowDialog(parent);
            if (!dialog.Confirmed) return;

            try
            {
                File.Delete(item.FullFilePath);
                GetSongLibrary()?.TriggerRefresh();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete library item {Path}", item.FullFilePath);
            }
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item == null || !File.Exists(item.FullFilePath)) return;
            var dir = Path.GetDirectoryName(item.FullFilePath)!;
            var nameNoExt = Path.GetFileNameWithoutExtension(item.FullFilePath);
            var ext = Path.GetExtension(item.FullFilePath);
            var dest = Path.Combine(dir, nameNoExt + " (Copy)" + ext);
            int i = 2;
            while (File.Exists(dest))
                dest = Path.Combine(dir, $"{nameNoExt} (Copy {i++}){ext}");
            try
            {
                File.Copy(item.FullFilePath, dest);
                GetSongLibrary()?.TriggerRefresh();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to duplicate library item {Path}", item.FullFilePath);
            }
        }

        private async void RenameItem_OnClick(object? sender, RoutedEventArgs e)
        {
            var item = GetClickedItem(sender);
            if (item == null || !File.Exists(item.FullFilePath)) return;
            var parent = TopLevel.GetTopLevel(this) as Window;
            if (parent == null) return;

            var dialog = new RenameDialog(Path.GetFileNameWithoutExtension(item.FullFilePath));
            await dialog.ShowDialog(parent);
            if (dialog.ResultName == null) return;

            var dir = Path.GetDirectoryName(item.FullFilePath)!;
            var ext = Path.GetExtension(item.FullFilePath);
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = string.Concat(dialog.ResultName.Select(c => invalidChars.Contains(c) ? '_' : c));
            var newPath = Path.Combine(dir, safeName + ext);
            if (newPath == item.FullFilePath) return;

            try
            {
                File.Move(item.FullFilePath, newPath);
                GetSongLibrary()?.TriggerRefresh();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to rename library item {Path} → {NewPath}", item.FullFilePath, newPath);
            }
        }
    }
}