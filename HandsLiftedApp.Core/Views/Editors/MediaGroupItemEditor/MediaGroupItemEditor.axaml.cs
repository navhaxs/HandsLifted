using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors.MediaGroupItemEditor
{
    public partial class MediaGroupItemEditor : UserControl
    {
        private const string DragDataFormat = "MediaGroupItemDrag";
        private MediaGroupItem.GroupItem? _dragSourceItem;
        private Point _pointerPressedPoint;
        private bool _isDragging;
        private int _lastHoveredIndex = -1;
        private bool _dropBefore;
        private Control? _lastAdornerElement;

        public MediaGroupItemEditor()
        {
            InitializeComponent();
            SetupDnd();
        }

        private void SetupDnd()
        {
            Thumbstrip.AddHandler(PointerPressedEvent, Thumbstrip_PointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            Thumbstrip.AddHandler(PointerMovedEvent, Thumbstrip_PointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            Thumbstrip.AddHandler(PointerReleasedEvent, Thumbstrip_PointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            Thumbstrip.AddHandler(DragDrop.DropEvent, OnThumbstripDrop);
            Thumbstrip.AddHandler(DragDrop.DragOverEvent, OnThumbstripDragOver);
            Thumbstrip.AddHandler(DragDrop.DragLeaveEvent, OnThumbstripDragLeave);
        }

        private static ListBoxItem? FindListBoxItem(Control? control)
        {
            while (control != null)
            {
                if (control is ListBoxItem lbi) return lbi;
                control = control.Parent as Control;
            }
            return null;
        }

        private void Thumbstrip_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(Thumbstrip).Properties.IsLeftButtonPressed) return;

            var lbi = FindListBoxItem(e.Source as Control);
            if (lbi?.DataContext is MediaGroupItem.GroupItem item)
            {
                _pointerPressedPoint = e.GetPosition(Thumbstrip);
                _dragSourceItem = item;
                _isDragging = false;
            }
        }

        private async void Thumbstrip_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragSourceItem == null || _isDragging) return;

            var pos = e.GetPosition(Thumbstrip);
            if (Math.Abs(pos.X - _pointerPressedPoint.X) > 6 || Math.Abs(pos.Y - _pointerPressedPoint.Y) > 6)
            {
                var item = _dragSourceItem;
                _isDragging = true;
                _dragSourceItem = null;

                var data = new DataObject();
                data.Set(DragDataFormat, item);
                await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

                _isDragging = false;
            }
        }

        private void Thumbstrip_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragSourceItem = null;
        }

        private void CalculateDropTarget(DragEventArgs e)
        {
            var point = e.GetPosition(Thumbstrip);
            var containers = Thumbstrip.GetRealizedContainers().ToList();

            if (!containers.Any())
            {
                _lastHoveredIndex = -1;
                return;
            }

            Control? found = null;
            foreach (var c in containers)
            {
                var p = c.TranslatePoint(new Point(0, 0), Thumbstrip);
                if (p.HasValue && point.X >= p.Value.X && point.X < p.Value.X + c.Bounds.Width)
                {
                    found = c;
                    break;
                }
            }
            found ??= point.X <= 0 ? containers.First() : containers.Last();

            int foundIndex = Thumbstrip.ItemContainerGenerator.IndexFromContainer(found);
            var itemPos = found.TranslatePoint(new Point(0, 0), Thumbstrip);
            double midX = itemPos.HasValue ? itemPos.Value.X + found.Bounds.Width / 2 : 0;
            _dropBefore = point.X < midX;

            int targetIndex = _dropBefore ? foundIndex : foundIndex + 1;
            if (_lastHoveredIndex != targetIndex)
                ClearAdorner();

            _lastHoveredIndex = targetIndex;
            _lastAdornerElement = found;
        }

        private void ShowDropAdorner()
        {
            if (_lastAdornerElement == null) return;
            var adornerLayer = AdornerLayer.GetAdornerLayer(_lastAdornerElement);
            if (adornerLayer == null) return;

            var indicator = new Border
            {
                BorderThickness = _dropBefore ? new Thickness(2, 0, 0, 0) : new Thickness(0, 0, 2, 0),
                BorderBrush = new SolidColorBrush(Color.Parse("#9a93cd")),
                IsHitTestVisible = false
            };
            adornerLayer.Children.Add(indicator);
            AdornerLayer.SetAdornedElement(indicator, _lastAdornerElement);
        }

        private void ClearAdorner()
        {
            if (_lastAdornerElement == null) return;
            AdornerLayer.GetAdornerLayer(_lastAdornerElement)?.Children.Clear();
            _lastAdornerElement = null;
        }

        private void OnThumbstripDragOver(object? sender, DragEventArgs e)
        {
            if (!e.Data.Contains(DragDataFormat))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }
            e.DragEffects = DragDropEffects.Move;
            CalculateDropTarget(e);
            ShowDropAdorner();
        }

        private void OnThumbstripDrop(object? sender, DragEventArgs e)
        {
            if (!e.Data.Contains(DragDataFormat)) return;

            var draggedItem = e.Data.Get(DragDataFormat) as MediaGroupItem.GroupItem;
            if (draggedItem == null) return;

            CalculateDropTarget(e);
            ClearAdorner();

            if (DataContext is not MediaGroupItemInstance vm) return;

            int sourceIndex = vm.Items.IndexOf(draggedItem);
            if (sourceIndex < 0 || _lastHoveredIndex < 0) return;

            int targetIndex = _lastHoveredIndex;
            if (targetIndex > sourceIndex) targetIndex--;
            if (sourceIndex == targetIndex) return;

            vm.Items.Move(sourceIndex, targetIndex);
        }

        private void OnThumbstripDragLeave(object? sender, RoutedEventArgs e)
        {
            ClearAdorner();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                if (mediaGroupItemInstance.Items.Count > 0)
                {
                    Thumbstrip.SelectedIndex = 0;
                }
            }
        }

        private void LoadButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Open();
        }

        private async void Open()
        {
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false
            });

            var filePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;

            if (filePath == null) return;

            XmlSerializer serializer = new XmlSerializer(typeof(MediaGroupItem));

            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                var x = serializer.Deserialize(stream);
                if (x != null && x is MediaGroupItem mediaGroupItem)
                {
                    // var itemInstance = ItemInstanceFactory.ToItemInstance((MediaGroupItem)x, null);
                    if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
                    {
                        mediaGroupItemInstance.SelectedSlideIndex = 0;
                        mediaGroupItemInstance.Items = mediaGroupItem.Items;
                    }
                }
            }
        }

        private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Save();
        }

        private async void Save()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File",
            });

            if (files is null) return;

            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                Item convertMe = HandsLiftedDocXmlSerializer.SerializeItem(mediaGroupItemInstance, null);
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(MediaGroupItem));
                    serializer.Serialize(memoryStream, convertMe);

                    // serialization was successful - only now do we write to disk
                    await using var stream = await files.OpenWriteAsync();

                    memoryStream.WriteTo(stream);
                }
            }
        }

        private void Duplicate_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                if (Thumbstrip.SelectedItem is MediaGroupItem.GroupItem selectedItem)
                {
                    var duplicatedSlide = selectedItem.Clone();
                    mediaGroupItemInstance.Items.Insert(Thumbstrip.SelectedIndex + 1, duplicatedSlide);
                }
            }
        }

        private void RemoveItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                if (Thumbstrip.SelectedItem is MediaGroupItem.GroupItem selectedItem)
                {
                    mediaGroupItemInstance.Items.Remove(selectedItem);
                }
            }
        }

        private void AddSlide_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                mediaGroupItemInstance.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            }
        }

        private void AddMedia_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                mediaGroupItemInstance.Items.Add(new MediaGroupItem.MediaItem() { });
            }
        }

        private void MoveUpItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: MediaGroupItem.SlideItem currentItem })
            {
                if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
                {
                    var currentItemIndex = mediaGroupItemInstance.Items.IndexOf(currentItem);
                    mediaGroupItemInstance.Items.Move(currentItemIndex, Math.Max(currentItemIndex - 1, 0));
                }
            }
        }

        private void MoveDownItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: MediaGroupItem.SlideItem currentItem })
            {
                if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
                {
                    var currentItemIndex = mediaGroupItemInstance.Items.IndexOf(currentItem);
                    mediaGroupItemInstance.Items.Move(currentItemIndex,
                        Math.Min(currentItemIndex + 1, mediaGroupItemInstance.Items.Count - 1));
                }
            }
        }

        private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MediaGroupItemInstance mediaGroupItemInstance)
            {
                mediaGroupItemInstance.GenerateSlides();
            }
        }
    }
}