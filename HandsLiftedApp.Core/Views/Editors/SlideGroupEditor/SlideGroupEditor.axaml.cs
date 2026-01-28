using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors.SlideGroupEditor
{
    public partial class SlideGroupEditor : UserControl
    {
        public SlideGroupEditor()
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
    }
}