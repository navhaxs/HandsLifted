using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.Library;

namespace HandsLiftedApp.Views.Library
{
    public partial class LibraryPaneView : UserControl
    {
        private const double DragThreshold = 4.0;
        private Point? _dragStart;
        private LibraryItem? _pendingDragItem;
        private Control? _pendingDragControl;

        public LibraryPaneView()
        {
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

            var dragData = new DataObject();
            var topLevel = TopLevel.GetTopLevel(this);
            IStorageFile file = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FullFilePath));
            dragData.Set(DataFormats.Files, new[] { file });

            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
        }

        private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: Core.Models.Library.Library library })
                Process.Start(new ProcessStartInfo(library.Config.Directory) { UseShellExecute = true });
        }
    }
}