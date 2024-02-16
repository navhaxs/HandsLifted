using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DynamicData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class GroupItemsEditor : UserControl
    {
        public GroupItemsEditor()
        {
            InitializeComponent();
// #if DEBUG
//             this.AttachDevTools();
// #endif

            if (Design.IsDesignMode)
                return;

            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            // this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            this.DataContextChanged += GroupItemsEditorWindow_DataContextChanged;
        }

        private async Task ShowOpenFileDialog(InteractionContext<Unit, string[]?> interaction)
        {
            // var dialog = new OpenFileDialog() { AllowMultiple = true }; // TODO Pass as flag
            // var fileNames = await dialog.ShowAsync(this);
            // interaction.SetOutput(fileNames);
        }

        private void GroupItemsEditorWindow_DataContextChanged(object? sender, System.EventArgs e)
        {
            //this.FindControl<DataGrid>("DataGrid").ItemsSource = ViewModel.Item.Slides;
        }

        public void AddItemFolderClick(object? sender, RoutedEventArgs args)
        {
        }

        // public void MoveItemUpClick(object? sender, RoutedEventArgs args)
        // {
        //     if (DataGrid_Items.SelectedIndex > -1)
        //     {
        //         updateSelectedIndex(DataGrid_Items.SelectedIndex, DataGrid_Items.SelectedIndex - 1);
        //     }
        // }
        //
        // public void MoveItemDownClick(object? sender, RoutedEventArgs args)
        // {
        //     if (DataGrid_Items.SelectedIndex > -1)
        //     {
        //         updateSelectedIndex(DataGrid_Items.SelectedIndex, DataGrid_Items.SelectedIndex + 1);
        //     }
        // }

        void updateSelectedIndex(int fromIndex, int nextIndex)
        {
            SlidesGroupItem target = (SlidesGroupItem)this.DataContext;

            if (target == null)
                return;

            nextIndex = (nextIndex + target.Items.Count) % target.Items.Count;

            if (nextIndex > -1 &&
                nextIndex < target.Items.Count)
            {
                target.Items.Move(fromIndex, nextIndex);
                // DataGrid_Items.ItemsSource = null;
                // DataGrid_Items.ItemsSource = target.Items;
                // DataGrid_Items.SelectedIndex = nextIndex;
            }
        }

        private void AddItemButton_OnClick(object? sender, RoutedEventArgs e)
        {
            AddItemsWorkflow();
        }

        private async Task AddItemsWorkflow()
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Media File(s)",
                AllowMultiple = true
            });

            foreach (var fileName in files)
            {
                // Put your logic for opening file here.
                //if (SUPPORTED_VIDEO.Contains(extNoDot) || SUPPORTED_IMAGE.Contains(extNoDot))
                //{
                //    SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroupItem = createMediaGroupItem(fileName);

                //    if (slidesGroupItem != null)
                //        addedItems.Add(slidesGroupItem);
                //}
                // var x = CreateItem.GenerateMediaContentSlide(fileName.Path);
                if (this.DataContext is MediaGroupItem mediaGroupItem)
                {
                    mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                        { SourceMediaFilePath = fileName.Path.AbsolutePath });
                }
            }
        }

        private void MoveItemUpButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItem mediaGroupItem)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(listBox.SelectedIndex) == null || mediaGroupItem.Items.ElementAtOrDefault(listBox.SelectedIndex - 1) == null)
                {
                    return;
                }

                mediaGroupItem.Items.Move(listBox.SelectedIndex, listBox.SelectedIndex - 1);
            }
        }

        private void MoveItemDownButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItem mediaGroupItem)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(listBox.SelectedIndex) == null || mediaGroupItem.Items.ElementAtOrDefault(listBox.SelectedIndex + 1) == null)
                {
                    return;
                }

                mediaGroupItem.Items.Move(listBox.SelectedIndex, listBox.SelectedIndex + 1);
            }
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem)
            {
                mediaGroupItem.GenerateSlides();
            }
        }

        private void RemoveItemButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItem mediaGroupItem)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(listBox.SelectedIndex) == null)
                {
                    return;
                }

                mediaGroupItem.Items.RemoveAt(listBox.SelectedIndex);
            }
        }
    }
}