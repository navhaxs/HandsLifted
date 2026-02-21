using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
                    mediaGroupItemInstance.Items.Move(currentItemIndex, Math.Min(currentItemIndex + 1, mediaGroupItemInstance.Items.Count - 1));
                }
            }
        }
    }
}