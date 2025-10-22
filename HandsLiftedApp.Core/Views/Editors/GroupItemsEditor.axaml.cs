using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class GroupItemsEditor : UserControl
    {
        private bool _isGridView = false;
        private int _selectedIndex = -1;
        private object? _draggedItem = null;
        private Border? _dropIndicator = null;
        private AdornerLayer? _adornerLayer = null;

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
            
            var itemsControl = this.FindControl<ItemsControl>("itemsControl");
            if (itemsControl != null)
            {
                DragDrop.SetAllowDrop(itemsControl, true);
                itemsControl.AddHandler(DragDrop.DragOverEvent, ItemsControl_DragOver);
                itemsControl.AddHandler(DragDrop.DropEvent, ItemsControl_Drop);
                itemsControl.AddHandler(DragDrop.DragLeaveEvent, ItemsControl_DragLeave);
                
                _adornerLayer = AdornerLayer.GetAdornerLayer(itemsControl);
                InitializeDropIndicator();
            }
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

        private void ViewToggle_OnClick(object? sender, RoutedEventArgs e)
        {
            var listViewToggle = this.FindControl<ToggleButton>("ListViewToggle");
            var gridViewToggle = this.FindControl<ToggleButton>("GridViewToggle");

            if (sender is ToggleButton button && listViewToggle != null && gridViewToggle != null)
            {
                if (button == listViewToggle)
                {
                    _isGridView = false;
                    listViewToggle.IsChecked = true;
                    gridViewToggle.IsChecked = false;
                    SetListView();
                }
                else if (button == gridViewToggle)
                {
                    _isGridView = true;
                    listViewToggle.IsChecked = false;
                    gridViewToggle.IsChecked = true;
                    SetGridView();
                }
            }
        }

        private void SetListView()
        {
            var itemsControl = this.FindControl<ItemsControl>("itemsControl");
            if (itemsControl == null) return;

            // Create StackPanel template using FuncTemplate
            var stackPanelTemplate = new FuncTemplate<Panel?>(() => new StackPanel());
            itemsControl.ItemsPanel = stackPanelTemplate;
            
            // Use the default ItemTemplate (which is the list template)
            itemsControl.ItemTemplate = itemsControl.Resources["ListTemplateSelector"] as IDataTemplate;
        }

        private void SetGridView()
        {
            var itemsControl = this.FindControl<ItemsControl>("itemsControl");
            if (itemsControl == null) return;

            // Create UniformGrid template using FuncTemplate
            var gridTemplate = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 4 });
            itemsControl.ItemsPanel = gridTemplate;
            
            // Switch to grid template selector
            itemsControl.ItemTemplate = itemsControl.Resources["GridTemplateSelector"] as IDataTemplate;
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

        private void AddItemCustomButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem)
            {
                var newSlideData = new CustomSlide();
                mediaGroupItem.Items.Add(new MediaGroupItem.SlideItem()
                    { SlideData = newSlideData });
            }
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
                if (this.DataContext is MediaGroupItemInstance mediaGroupItem)
                {
                    mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                        { SourceMediaFilePath = fileName.Path.LocalPath });
                }
            }
        }

        private void MoveItemUpButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem && _selectedIndex > 0)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex) == null || mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex - 1) == null)
                {
                    return;
                }

                var finalIndex = _selectedIndex - 1;
                mediaGroupItem.Items.Move(_selectedIndex, finalIndex);
                _selectedIndex = finalIndex;
            }
        }

        private void MoveItemDownButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem && _selectedIndex >= 0 && _selectedIndex < mediaGroupItem.Items.Count - 1)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex) == null || mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex + 1) == null)
                {
                    return;
                }

                var finalIndex = _selectedIndex + 1;
                mediaGroupItem.Items.Move(_selectedIndex, finalIndex);
                _selectedIndex = finalIndex;
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
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem && _selectedIndex >= 0)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex) == null)
                {
                    return;
                }

                mediaGroupItem.Items.RemoveAt(_selectedIndex);
                _selectedIndex = -1; // Reset selection
            }
        }

        private void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem && _selectedIndex >= 0)
            {
                if (mediaGroupItem.Items.ElementAtOrDefault(_selectedIndex) is MediaGroupItem.SlideItem slideItem)
                {
                    SlideEditorWindow slideEditorWindow = new() { DataContext = new FreeTextSlideEditorViewModel() { Slide = slideItem.SlideData }};
                    slideEditorWindow.Show();
                }
            }
        }

        private void Duplicate_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MediaGroupItemInstance mediaGroupItem)
            {
                if (sender is MenuItem menuItem && menuItem.DataContext is MediaGroupItem.GroupItem item)
                {
                    var index = mediaGroupItem.Items.IndexOf(item);
                    if (index >= 0)
                    {
                        try
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(MediaGroupItem.GroupItem));
                            using (StringWriter writer = new())
                            {
                                serializer.Serialize(writer, item);
                                var obj = writer.ToString();

                                using (StringReader reader = new StringReader(obj))
                                {
                                    var duplicatedItem = serializer.Deserialize(reader) as MediaGroupItem.GroupItem;
                                    if (duplicatedItem != null)
                                    {
                                        mediaGroupItem.Items.Insert(index + 1, duplicatedItem);
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            // Handle serialization errors gracefully
                            System.Diagnostics.Debug.WriteLine($"Failed to duplicate item: {ex.Message}");
                        }
                    }
                }
            }
        }

        private async void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is MediaGroupItem.GroupItem item && e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                _draggedItem = item;
                var dragData = new DataObject();
                dragData.Set("application/x-avalonia-reorder", item);
                
                var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
                if (result == DragDropEffects.Move)
                {
                    _draggedItem = null;
                }
            }
        }

        private void ItemsControl_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains("application/x-avalonia-reorder"))
            {
                e.DragEffects = DragDropEffects.Move;
                ShowDropIndicator(e.GetPosition(sender as Control));
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                HideDropIndicator();
            }
        }

        private void ItemsControl_Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains("application/x-avalonia-reorder") && 
                _draggedItem is MediaGroupItem.GroupItem draggedItem &&
                this.DataContext is MediaGroupItemInstance mediaGroupItem)
            {
                var targetElement = e.Source as Control;
                while (targetElement != null && targetElement.DataContext is not MediaGroupItem.GroupItem)
                {
                    targetElement = targetElement.Parent as Control;
                }
                
                if (targetElement?.DataContext is MediaGroupItem.GroupItem targetItem)
                {
                    var draggedIndex = mediaGroupItem.Items.IndexOf(draggedItem);
                    var targetIndex = mediaGroupItem.Items.IndexOf(targetItem);
                    
                    if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
                    {
                        mediaGroupItem.Items.Move(draggedIndex, targetIndex);
                    }
                }
            }
            _draggedItem = null;
            HideDropIndicator();
        }

        private void ItemsControl_DragLeave(object? sender, DragEventArgs e)
        {
            HideDropIndicator();
        }

        private void InitializeDropIndicator()
        {
            _dropIndicator = new Border
            {
                Background = Brushes.Blue,
                Height = 3,
                IsVisible = false,
                ZIndex = 1000
            };
        }

        private void ShowDropIndicator(Point position)
        {
            if (_dropIndicator == null || _adornerLayer == null) return;

            var itemsControl = this.FindControl<ItemsControl>("itemsControl");
            if (itemsControl?.ItemsPanelRoot == null) return;

            var targetElement = GetDropTarget(position, itemsControl);
            if (targetElement == null) return;

            var bounds = targetElement.Bounds;
            var relativePosition = targetElement.TranslatePoint(new Point(0, 0), itemsControl) ?? new Point(0, 0);

            if (_isGridView)
            {
                // For grid view, show vertical line at left edge of target
                _dropIndicator.Width = 3;
                _dropIndicator.Height = bounds.Height;
                Canvas.SetLeft(_dropIndicator, relativePosition.X);
                Canvas.SetTop(_dropIndicator, relativePosition.Y);
            }
            else
            {
                // For list view, show horizontal line at top edge of target
                _dropIndicator.Width = itemsControl.Bounds.Width;
                _dropIndicator.Height = 3;
                Canvas.SetLeft(_dropIndicator, 0);
                Canvas.SetTop(_dropIndicator, relativePosition.Y);
            }

            if (!_dropIndicator.IsVisible)
            {
                AdornerLayer.SetAdornedElement(_dropIndicator, itemsControl);
                _adornerLayer.Children.Add(_dropIndicator);
                _dropIndicator.IsVisible = true;
            }
        }

        private void HideDropIndicator()
        {
            if (_dropIndicator != null && _adornerLayer != null && _dropIndicator.IsVisible)
            {
                _adornerLayer.Children.Remove(_dropIndicator);
                _dropIndicator.IsVisible = false;
            }
        }

        private Control? GetDropTarget(Point position, ItemsControl itemsControl)
        {
            var hitTest = itemsControl.InputHitTest(position);
            var element = hitTest as Control;
            
            while (element != null && element.DataContext is not MediaGroupItem.GroupItem)
            {
                element = element.Parent as Control;
            }
            
            return element;
        }
    }
}