using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.LibraryView
{
    public partial class MediaLibraryQueryView : UserControl
    {
        private const double DragThreshold = 4.0;
        private Point? _dragStart;
        private HomeLibraryEntry? _pendingDragItem;
        private Control? _pendingDragControl;
        private PointerPressedEventArgs? _pendingDragPressedArgs;

        public MediaLibraryQueryView()
        {
            InitializeComponent();
        }

        private MediaLibraryQueryViewModel? Vm => DataContext as MediaLibraryQueryViewModel;

        private static HomeLibraryEntry? GetEntry(object? sender) =>
            sender is Control { DataContext: HomeLibraryEntry entry } ? entry : null;

        private void Entry_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry is { IsDirectory: true })
            {
                Vm?.NavigateIntoCommand.Execute(entry).Subscribe();
            }
        }

        private void Breadcrumb_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: BreadcrumbSegment segment })
            {
                Vm?.NavigateToBreadcrumbCommand.Execute(segment.RelativePath).Subscribe();
            }
        }

        private void AddToPlaylist_OnClick(object? sender, RoutedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry == null || entry.IsDirectory) return;

            MessageBus.Current.SendMessage(new AddItemByFilePathMessage(new List<string> { entry.FullPath }));
        }

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
            }
        }

        // Drag-to-add for file tiles only, mirroring LibraryQueryView's Media Bin drag behavior.
        private void Entry_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry is not { IsDirectory: false } || sender is not Control control) return;

            _dragStart = e.GetPosition(null);
            _pendingDragItem = entry;
            _pendingDragControl = control;
            _pendingDragPressedArgs = e;
            control.PointerMoved += Entry_OnPointerMoved;
            control.PointerReleased += Entry_OnPointerReleased;
            e.Pointer.Capture(control);
        }

        private void Entry_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            CancelPendingDrag();
        }

        private void CancelPendingDrag()
        {
            if (_pendingDragControl != null)
            {
                _pendingDragControl.PointerMoved -= Entry_OnPointerMoved;
                _pendingDragControl.PointerReleased -= Entry_OnPointerReleased;
            }
            _dragStart = null;
            _pendingDragItem = null;
            _pendingDragControl = null;
            _pendingDragPressedArgs = null;
        }

        private async void Entry_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStart == null || _pendingDragItem == null || _pendingDragPressedArgs == null) return;

            var delta = e.GetPosition(null) - _dragStart.Value;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

            var item = _pendingDragItem;
            var pressedArgs = _pendingDragPressedArgs;
            CancelPendingDrag();

            var dragData = new DataTransfer();
            var topLevel = TopLevel.GetTopLevel(this);
            IStorageFile file = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FullPath));
            dragData.Add(DataTransferItem.Create(DataFormat.File, file));

            await DragDrop.DoDragDropAsync(pressedArgs, dragData, DragDropEffects.Copy);
        }
    }
}
