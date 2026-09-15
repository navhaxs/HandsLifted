using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Controls
{
    public partial class ItemSlidesView : UserControl
    {
        public ItemSlidesView()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                //this.DataContext = new SectionHeadingItem<ItemStateImpl>();
                //this.DataContext = PlaylistUtils.CreateSong();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: if follow mode enabled
            // (and have UI to "recentre" like in google maps)
            //MessageBus.Current.SendMessage(new FocusSelectedItem());


            // TODO: 'on click' event - NOT just 'on clicked AND index has changed'
            // https://github.com/AvaloniaUI/Avalonia/discussions/7182
            //MessageBus.Current.SendMessage(new OnSelectionClickedMessage());
        }

        internal class OnSelectionClickedMessage
        {
            //public Item<ItemStateImpl> SourceItem { get; set; }
        }

        private void MoveUpItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                MessageBus.Current.SendMessage(new MoveItemCommand()
                    { SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.UP });
            }
        }

        private void MoveDownItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                MessageBus.Current.SendMessage(new MoveItemCommand()
                    { SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.DOWN });
            }
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                MessageBus.Current.SendMessage(new MoveItemCommand()
                    { SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.DUPLICATE });
            }
        }

        private void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                // TODO confirmation window

                MessageBus.Current.SendMessage(new MoveItemCommand()
                    { SourceItem = (Item)control.DataContext, Direction = MoveItemCommand.DirectionValue.REMOVE });
            }
        }

        private void ItemBorder_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ((Border)sender).Classes.Add("fade-in");
        }

        private int FindItemIndexOf(Grid dropContainer, DragEventArgs e)
        {
            var listBox = dropContainer.FindDescendantOfType<ListBoxWithoutKey>();
            Debug.Print(e.GetPosition(listBox).ToString());

            var matchIdx = -1;
            // work out where we are dragging to

            if (listBox.Items.Count == 0)
            {
                // always target first element if empty
                return 0;
            }

            for (var idx = 0; idx < listBox.Items.Count; idx++)
            {
                var listBoxItem = listBox.ContainerFromIndex(idx);
                var relativePos = e.GetPosition(listBoxItem);
                var isIntersect = relativePos.X <= listBoxItem.Bounds.Width
                                  && relativePos.Y <= listBoxItem.Bounds.Height;
                if (isIntersect)
                {
                    matchIdx = idx;
                    break;
                }

                bool isVeryLastItem = idx == listBox.Items.Count - 1;
                if (isVeryLastItem)
                {
                    matchIdx = listBox.Items.Count;
                    break;
                }

                bool isPosWithinCurrentRow = relativePos.Y <= listBoxItem.Bounds.Height;
                if (!isPosWithinCurrentRow)
                    continue;

                // lookahead to next item
                var nextListBoxItem = listBox.ContainerFromIndex(idx + 1);
                bool isLastColInRow = !nextListBoxItem?.Bounds.Y.Equals(listBoxItem.Bounds.Y) ?? false;
                if (isLastColInRow)
                {
                    matchIdx = idx + 1;
                    break;
                }

                // Debug.Print($"{idx} {relativePos.ToString()} {isIntersect}");
            }

            Debug.Print($"MatchIdx={matchIdx}");
            return matchIdx;
        }

        void SetupDnd(Grid dropContainer)
        {
            Control? lastAdornerElement = null;

            void ClearAdorner()
            {
                if (lastAdornerElement == null) return;
                AdornerLayer.GetAdornerLayer(lastAdornerElement)?.Children.Clear();
                lastAdornerElement = null;
            }

            void ShowInsertAdorner(int insertIndex)
            {
                var listBox = dropContainer.FindDescendantOfType<ListBoxWithoutKey>();
                if (listBox == null || insertIndex < 0) return;

                Control? target;
                bool onRight;

                if (insertIndex >= listBox.Items.Count)
                {
                    target = listBox.ContainerFromIndex(listBox.Items.Count - 1);
                    onRight = true;
                }
                else
                {
                    target = listBox.ContainerFromIndex(insertIndex);
                    onRight = false;
                }

                if (target == null || target == lastAdornerElement) return;

                ClearAdorner();

                var adornerLayer = AdornerLayer.GetAdornerLayer(target);
                if (adornerLayer == null) return;

                var indicator = new Border
                {
                    BorderThickness = onRight ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.Parse("#9a93cd")),
                    IsHitTestVisible = false
                };
                adornerLayer.Children.Add(indicator);
                AdornerLayer.SetAdornedElement(indicator, target);
                lastAdornerElement = target;
            }

            void DragOver(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                    e.DragEffects = e.DragEffects & DragDropEffects.Move;
                else
                    e.DragEffects = e.DragEffects & DragDropEffects.Copy;

                if (!e.DataTransfer.Contains(DataFormat.Text)
                    && !e.DataTransfer.Contains(DataFormat.File)
                    && !e.DataTransfer.Contains(SlideDragDropCustomDataFormat.Format))
                {
                    e.DragEffects = DragDropEffects.None;
                    return;
                }

                if (e.DataTransfer.Contains(SlideDragDropCustomDataFormat.Format))
                {
                    var insertIndex = FindItemIndexOf(dropContainer, e);
                    ShowInsertAdorner(insertIndex);
                }
            }

            void DragLeave(object? sender, DragEventArgs e)
            {
                ClearAdorner();
            }

            async void Drop(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                    e.DragEffects = e.DragEffects & DragDropEffects.Move;
                else
                    e.DragEffects = e.DragEffects & DragDropEffects.Copy;

                ClearAdorner();

                var destSlideIndex = FindItemIndexOf(dropContainer, e);

                if (destSlideIndex > -1 && sender is Control { DataContext: Item destItem })
                {
                    if (e.DataTransfer.Contains(SlideDragDropCustomDataFormat.Format))
                    {
                        var sourceSlideReference =
                            e.DataTransfer.TryGetValue(SlideDragDropCustomDataFormat.Format)!;

                        MessageBus.Current.SendMessage(new MoveSlideCommand()
                        {
                            SourceItemUUID = sourceSlideReference.SourceItemUUID,
                            SourceSlideIndex = sourceSlideReference.SourceSlideIndex,
                            DestItemUUID = destItem.UUID,
                            DestSlideIndex = destSlideIndex
                        });
                    }
                    else if (e.DataTransfer.Contains(DataFormat.File))
                    {
                        var files = e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>();
                        MessageBus.Current.SendMessage(new AddFilesToGroupItemCommand()
                        {
                            SourceFiles = files,
                            DestItemUUID = destItem.UUID,
                            DestSlideIndex = destSlideIndex
                        });
                    }
                }
            }

            dropContainer.AddHandler(DragDrop.DropEvent, Drop);
            dropContainer.AddHandler(DragDrop.DragOverEvent, DragOver);
            dropContainer.AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        }

        private void DropContainer_OnAttachedToLogicalTree(object? sender, LogicalTreeAttachmentEventArgs e)
        {
            SetupDnd(sender as Grid);
            SetupHoverInsert(sender as Grid);
        }

        // Hover-reveal "+" button in the gaps between slide thumbnails (and before the
        // first / after the last), for inserting a new MediaGroupItem.GroupItem via the
        // same "Add Media" / "Add Custom Slide" options MediaGroupItemEditor offers.
        // Only applies to MediaGroupItemInstance - other item types don't support arbitrary insertion.
        //
        // The button lives on "InsertOverlay", a plain Canvas sibling of the ListBoxWithoutKey
        // with no Background set, so it never intercepts hit-testing itself (only its Button
        // child does) - existing slide selection/DnD on the list underneath is unaffected.
        // Position is computed as the true midpoint between two adjacent slide containers
        // (via TranslatePoint into the overlay's own coordinate space) rather than being
        // anchored to one container's edge, so it lands in the visual gap instead of overlapping
        // thumbnail content. "Nearest gap, with hysteresis" hit-testing (bigger radius to leave
        // than to enter) avoids the flicker a hard edge/boundary test causes once the button
        // itself is hovered.
        void SetupHoverInsert(Grid dropContainer)
        {
            if (dropContainer?.DataContext is not MediaGroupItemInstance mediaGroupItemInstance) return;

            var overlay = dropContainer.Children.OfType<Canvas>().FirstOrDefault(c => c.Name == "InsertOverlay");
            if (overlay == null) return;

            const double buttonSize = 22;
            const double enterRadius = 22;
            const double exitRadius = 34;

            Button? insertButton = null;
            int? shownInsertIndex = null;

            MenuFlyout BuildInsertFlyout(Func<int> resolveInsertIndex)
            {
                var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };

                var addMedia = new MenuItem { Header = "Add Media" };
                addMedia.Click += async (_, _) =>
                {
                    // Unlike a SlideItem/CustomSlide, a MediaItem with no SourceMediaFilePath
                    // yet renders no Slide at all (CreateItem.GenerateMediaContentSlide returns
                    // null for it), so GenerateSlides() would just silently drop it - browse for
                    // the file up front instead, matching MediaItemEditor's BrowseButton_OnClick.
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return;

                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select Media File",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Image files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } },
                            new FilePickerFileType("Video files") { Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.webm" } },
                            new FilePickerFileType("All files") { Patterns = new[] { "*.*" } },
                        }
                    });

                    var filePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
                    if (filePath == null) return;

                    string localizedMediaPath;
                    try
                    {
                        localizedMediaPath = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(
                            filePath, Globals.Instance.AppPreferences?.MediaLibraryPath);
                    }
                    catch (MediaLibraryNotConfiguredException ex)
                    {
                        Log.Warning(ex, "Cannot add media to group: Media Library not configured");
                        GoogleSlidesReauthWindow.ShowError("Media Library Not Configured", ex.Message);
                        return;
                    }

                    var index = Math.Clamp(resolveInsertIndex(), 0, mediaGroupItemInstance.Items.Count);
                    mediaGroupItemInstance.Items.Insert(index, new MediaGroupItem.MediaItem { SourceMediaFilePath = localizedMediaPath });
                    mediaGroupItemInstance.GenerateSlides();
                };

                var addSlide = new MenuItem { Header = "Add Custom Slide" };
                addSlide.Click += (_, _) =>
                {
                    var index = Math.Clamp(resolveInsertIndex(), 0, mediaGroupItemInstance.Items.Count);
                    mediaGroupItemInstance.Items.Insert(index, new MediaGroupItem.SlideItem { SlideData = new CustomSlide() });
                    mediaGroupItemInstance.GenerateSlides();
                };

                flyout.Items.Add(addMedia);
                flyout.Items.Add(addSlide);
                return flyout;
            }

            Button EnsureButton()
            {
                if (insertButton != null) return insertButton;

                insertButton = new Button
                {
                    Width = buttonSize,
                    Height = buttonSize,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(buttonSize / 2),
                    Background = new SolidColorBrush(Color.Parse("#724bab")),
                    Foreground = Brushes.White,
                    Content = "+",
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsVisible = false
                };
                overlay.Children.Add(insertButton);
                return insertButton;
            }

            void HideButton()
            {
                if (insertButton != null) insertButton.IsVisible = false;
                shownInsertIndex = null;
            }

            void ShowButtonAt(Point center, int insertIndex)
            {
                var button = EnsureButton();
                if (shownInsertIndex != insertIndex)
                {
                    button.Flyout = BuildInsertFlyout(() => insertIndex);
                    shownInsertIndex = insertIndex;
                }

                Canvas.SetLeft(button, center.X - buttonSize / 2);
                Canvas.SetTop(button, center.Y - buttonSize / 2);
                button.IsVisible = true;
            }

            // One point per valid insertion index (0..count): the midpoint of the gap it
            // represents. A gap between two same-row items sits between their edges; a gap
            // before the first item in a (possibly wrapped) row sits just left of it.
            //
            // Positions come from "PART_Thumbnail" (the ListBoxItem control template's actual
            // thumbnail-image element), not the container's own rect, for both axes:
            //  - Y: the container also includes the slide number/label strip docked below the
            //    thumbnail, so centering on the container sits low, inside that strip.
            //  - X: PART_Thumbnail is HorizontalAlignment="Left" inside the container and keeps
            //    a fixed (16:9) aspect ratio, so a container can be wider than the thumbnail it
            //    holds - using the container's edges biases the midpoint towards whichever
            //    neighbour's container happens to have more trailing whitespace.
            List<(Point Center, int InsertIndex)> ComputeGapPoints(ListBoxWithoutKey listBox)
            {
                var points = new List<(Point Center, int InsertIndex)>();
                var count = listBox.Items.Count;

                var visualRects = new Rect?[count];
                for (var i = 0; i < count; i++)
                {
                    var container = listBox.ContainerFromIndex(i);
                    if (container == null) continue;

                    var thumbnail = container.GetVisualDescendants().OfType<Control>()
                        .FirstOrDefault(v => v.Name == "PART_Thumbnail");
                    var target = (Visual?)thumbnail ?? container;
                    var topLeft = target.TranslatePoint(new Point(0, 0), overlay) ?? default;
                    visualRects[i] = new Rect(topLeft, target.Bounds.Size);
                }

                for (var i = 0; i < count; i++)
                {
                    if (visualRects[i] is not { } r) continue;
                    var y = r.Y + r.Height / 2;

                    var sameRowAsPrevious = i > 0 && visualRects[i - 1] is { } prev && Math.Abs(prev.Y - r.Y) < 1;
                    points.Add(sameRowAsPrevious
                        ? (new Point((visualRects[i - 1]!.Value.Right + r.X) / 2, y), i)
                        : (new Point(Math.Max(r.X / 2, 0), y), i));

                    if (i == count - 1)
                    {
                        points.Add((new Point(r.Right + 16, y), count));
                    }
                }

                return points;
            }

            void PointerMoved(object? sender, PointerEventArgs e)
            {
                var listBox = dropContainer.FindDescendantOfType<ListBoxWithoutKey>();
                if (listBox == null || listBox.Items.Count == 0) return;

                var pos = e.GetPosition(overlay);

                (Point Center, int InsertIndex)? nearest = null;
                var nearestDistance = double.MaxValue;
                foreach (var gap in ComputeGapPoints(listBox))
                {
                    var dx = pos.X - gap.Center.X;
                    var dy = pos.Y - gap.Center.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = gap;
                    }
                }

                var radius = shownInsertIndex != null ? exitRadius : enterRadius;
                if (nearest != null && nearestDistance <= radius)
                {
                    ShowButtonAt(nearest.Value.Center, nearest.Value.InsertIndex);
                }
                else
                {
                    HideButton();
                }
            }

            void PointerExited(object? sender, PointerEventArgs e) => HideButton();

            Button? emptyStateButton = null;

            void UpdateEmptyState()
            {
                var listBox = dropContainer.FindDescendantOfType<ListBoxWithoutKey>();
                var isEmpty = listBox == null || listBox.Items.Count == 0;

                if (isEmpty && emptyStateButton == null)
                {
                    emptyStateButton = new Button
                    {
                        Width = buttonSize,
                        Height = buttonSize,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(buttonSize / 2),
                        Background = new SolidColorBrush(Color.Parse("#724bab")),
                        Foreground = Brushes.White,
                        Content = "+",
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Flyout = BuildInsertFlyout(() => 0)
                    };
                    dropContainer.Children.Add(emptyStateButton);
                }

                if (emptyStateButton != null)
                {
                    emptyStateButton.IsVisible = isEmpty;
                }

                if (!isEmpty)
                {
                    HideButton();
                }
            }

            dropContainer.AddHandler(InputElement.PointerMovedEvent, PointerMoved);
            dropContainer.AddHandler(InputElement.PointerExitedEvent, PointerExited);

            mediaGroupItemInstance.Slides.CollectionChanged += (_, _) => UpdateEmptyState();
            UpdateEmptyState();
        }
    }
}