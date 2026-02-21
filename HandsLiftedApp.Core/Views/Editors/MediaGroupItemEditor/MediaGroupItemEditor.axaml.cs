using System;
using System.IO;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors.MediaGroupItemEditor
{
    public partial class MediaGroupItemEditor : UserControl
    {
        public MediaGroupItemEditor()
        {
            InitializeComponent();
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
                    // TODO implement cloning
                    var duplicatedSlide = selectedItem; //.Clone();
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