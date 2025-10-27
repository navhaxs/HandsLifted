using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

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

        private void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SongItemInstance item })
            {
                SongEditorViewModel songEditorViewModel =
                    new SongEditorViewModel(item, Globals.Instance.MainViewModel.Playlist);
                // songEditorViewModel.SongDataUpdated += (ex, ey) =>
                // {
                //             
                // };
                SongEditorWindow songEditorWindow = new SongEditorWindow() { DataContext = songEditorViewModel };
                songEditorWindow.Show();
                return;
            }

            if (sender is Control { DataContext: MediaGroupItemInstance mediaGroupItemInstance })
            {
                GenericContentEditorWindow songEditorWindow = new GenericContentEditorWindow()
                    { DataContext = mediaGroupItemInstance };
                songEditorWindow.Show();
                return;
            }

            if (sender is Control { DataContext: PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance })
            {
                GenericContentEditorWindow songEditorWindow = new GenericContentEditorWindow()
                    { DataContext = pdfSlidesGroupItemInstance };
                songEditorWindow.Show();
                return;
            }

            if (sender is Control
                {
                    DataContext: PowerPointPresentationItemInstance powerPointPresentationItemInstance
                })
            {
                GenericContentEditorWindow songEditorWindow = new GenericContentEditorWindow()
                    { DataContext = powerPointPresentationItemInstance };
                songEditorWindow.Show();
                return;
            }
            
            if (sender is Control { DataContext: GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance })
            {
                GenericContentEditorWindow songEditorWindow = new GenericContentEditorWindow()
                    { DataContext = googleSlidesGroupItemInstance };
                songEditorWindow.Show();
                return;
            }
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

            var rowHeight = (listBox.ItemsPanelRoot as WrapPanel).ItemHeight;
            var colWidth = (listBox.ItemsPanelRoot as WrapPanel).ItemWidth;

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
            void DragOver(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                this.Background = SolidColorBrush.Parse("Red");

                // Only allow if the dragged data contains text or filenames.
                if (!e.Data.Contains(DataFormats.Text)
                    && !e.Data.Contains(DataFormats.Files)
                    && !e.Data.Contains(SlideDragDropCustomDataFormat.CustomFormat))
                    e.DragEffects = DragDropEffects.None;

                if (e.Data.Contains(SlideDragDropCustomDataFormat.CustomFormat))
                {
                    FindItemIndexOf(dropContainer, e);
                }
            }

            void DragLeave(object? sender, DragEventArgs e)
            {
                this.Background = SolidColorBrush.Parse("Transparent");
            }

            async void Drop(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                var destSlideIndex = FindItemIndexOf(dropContainer, e);

                if (destSlideIndex > -1 && sender is Control { DataContext: Item destItem })
                {
                    if (e.Data.Contains(SlideDragDropCustomDataFormat.CustomFormat))
                    {
                        var sourceSlideReference =
                            ((SlideDragDropCustomDataFormat)e.Data.Get(SlideDragDropCustomDataFormat.CustomFormat));

                        Debug.Print(sourceSlideReference.ToString());


                        MessageBus.Current.SendMessage(new MoveSlideCommand()
                        {
                            SourceItemUUID = sourceSlideReference.SourceItemUUID,
                            SourceSlideIndex = sourceSlideReference.SourceSlideIndex,
                            DestItemUUID = destItem.UUID,
                            DestSlideIndex = destSlideIndex
                        });
                    }
                    else if (e.Data.Contains(DataFormats.Text))
                    {
                        //_dropState.Text = e.Data.GetText();
                    }
                    else if (e.Data.Contains(DataFormats.Files))
                    {
                        var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();
                        MessageBus.Current.SendMessage(new AddFilesToGroupItemCommand()
                        {
                            SourceFiles = files,
                            DestItemUUID = destItem.UUID,
                            DestSlideIndex = destSlideIndex
                        });
                    }
#pragma warning disable CS0618 // Type or member is obsolete
                    else if (e.Data.Contains(DataFormats.FileNames))
                    {
                        var files = e.Data.GetFileNames();
                        // _dropState.Text = string.Join(Environment.NewLine, files ?? Array.Empty<string>());
                    }
#pragma warning restore CS0618 // Type or member is obsolete
                    //else if (e.Data.Contains(CustomFormat))
                    //{
                    //    _dropState.Text = "Custom: " + e.Data.Get(CustomFormat);
                    //}
                }

                this.Background = SolidColorBrush.Parse("Transparent");
            }

            //dragMe.PointerPressed += DoDrag;
            dropContainer.AddHandler(DragDrop.DropEvent, Drop);
            dropContainer.AddHandler(DragDrop.DragOverEvent, DragOver);
            dropContainer.AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        }

        private void DropContainer_OnAttachedToLogicalTree(object? sender, LogicalTreeAttachmentEventArgs e)
        {
            SetupDnd(sender as Grid);
        }
    }
}