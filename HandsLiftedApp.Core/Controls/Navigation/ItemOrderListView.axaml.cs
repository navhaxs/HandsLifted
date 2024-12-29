using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia.LogicalTree;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Controls.Navigation
{
    // TODO disable spyscroll feature as its rather buggy
    // good effort tho.
    public partial class ItemOrderListView : UserControl
    {
        private static readonly object syncSlidesLock = new object();

        ListBox listBox;

        public ItemOrderListView()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                return;
            }

            this.WhenAnyValue(x => x.Bounds)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(bounds =>
                {
                    Root.IsVisible = bounds.Width >= 70;
                });

            listBox = this.FindControl<ListBox>("itemsListBox");
            listBox.SelectionChanged += ListBox_SelectionChanged;

            MessageBus.Current.Listen<SpyScrollUpdateMessage>()
                .Subscribe(x =>
                {
                    lock (syncSlidesLock)
                    {
                        listBox.SelectionChanged -= ListBox_SelectionChanged;
                        listBox.SelectedIndex = x.Index;
                        listBox.SelectionChanged += ListBox_SelectionChanged;
                    }
                });


            //SetupDnd("Text", d => d.Set(DataFormats.Text, $"Text was dragged"), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

            //SetupDnd("Custom", d => d.Set(CustomFormat, "Test123"), DragDropEffects.Move);
            SetupDnd("Files",
                d => d.Set(DataFormats.FileNames,
                    new[] { Assembly.GetEntryAssembly()?.GetModules().FirstOrDefault()?.FullyQualifiedName }),
                DragDropEffects.Copy);

            MessageBus.Current.Listen<AddItemMessage>()
                .Subscribe(async addItemMessage => { });
        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (listBox.SelectedIndex > -1)
                MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = listBox.SelectedIndex });
        }

        // private Control lastHoveredListItem;
        int lastHoveredIndex = -1;
        private bool isUpper;
        Control lastAdornerElement;

        void SetupDnd(string suffix, Action<DataObject> factory, DragDropEffects effects)
        {
            void Calculate(object? sender, DragEventArgs e)
            {
                var point = e.GetPosition(sender as Control);

                var found = listBox.GetRealizedContainers().LastOrDefault(
                    (Func<Control, bool>)(container =>
                    {
                        return point.Y >= container.Bounds.Top && point.Y <= container.Bounds.Bottom;
                    }), listBox.GetRealizedContainers().Last());
                int foundIndex = listBox.ItemContainerGenerator.IndexFromContainer(found);

                var relativePoint = e.GetPosition(found);
                isUpper = relativePoint.Y < found.Bounds.Height / 2;

                int calculatedTargetIndex = isUpper ? foundIndex : foundIndex + 1;
                if (lastHoveredIndex != calculatedTargetIndex)
                {
                    clearLastAdornerLayer();
                }

                lastHoveredIndex = calculatedTargetIndex;
                lastAdornerElement = found;
            }

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

                // Only allow if the dragged data contains text or filenames.
                if (!e.Data.Contains(DataFormats.Text)
                    && !e.Data.Contains(DataFormats.Files)) {
                    //&& !e.Data.Contains(CustomFormat))
                    e.DragEffects = DragDropEffects.None;
                }

                Calculate(sender, e);
      
                var adornerLayer = AdornerLayer.GetAdornerLayer(lastAdornerElement);
                if (adornerLayer != null)
                {
                    var adornedElement = new Border()
                    {
                        CornerRadius = new CornerRadius(0, 0, 0, 0),
                        BorderThickness = isUpper ? new Thickness(0, 2, 0, 0) : new Thickness(0, 0, 0, 2), // L T R B
                        BorderBrush = new SolidColorBrush(Color.Parse("#9a93cd")),
                        IsHitTestVisible = false
                    };
                    adornerLayer.Children.Add(adornedElement);
                    AdornerLayer.SetAdornedElement(adornedElement, lastAdornerElement);
                }
            }

            void Drop(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                Calculate(sender, e);
                
                if (e.Data.Contains(DataFormats.Files))
                {
                    MessageBus.Current.SendMessage(new AddItemByFilePathMessage(e.Data.GetFileNames().ToList(), lastHoveredIndex != -1 ? lastHoveredIndex : null));
                }

                clearLastAdornerLayer();
            }

            void DragLeave(object? sender, RoutedEventArgs e)
            {
                clearLastAdornerLayer();
            }

            void clearLastAdornerLayer()
            {
                if (lastAdornerElement != null)
                {
                    var adornerLayer = AdornerLayer.GetAdornerLayer(lastAdornerElement);
                    adornerLayer?.Children.Clear();
                    lastAdornerElement = null;
                }
            }
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
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

        private void AddItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                int? insertIndex;
                if (control.DataContext is Item SourceItem)
                {
                    insertIndex = Globals.Instance.MainViewModel.Playlist.Items.IndexOf(SourceItem) + 1;
                }
                else
                {
                    insertIndex = Globals.Instance.MainViewModel.Playlist.Items.Count;
                }

                HandleAddItemButtonClick.ShowAddWindow(insertIndex, sender);
            }
        }

        private void AddContentButton_OnClick(object? sender, RoutedEventArgs e)
        {
            HandleAddItemButtonClick.ShowAddWindow(null, sender);
        }

        object __lockObj = new();
        private List<StyledElement> _attachedListBoxItems = new();

        private void StyledElement_OnAttachedToLogicalTree(object? sender, LogicalTreeAttachmentEventArgs e)
        {
            clearMenuOpenClasses();

            if (sender is Control control)
            {
                var listBoxItem = control?.Parent?.Parent;
                if (listBoxItem is not null)
                {
                    lock (__lockObj)
                    {
                        _attachedListBoxItems.Add(listBoxItem);
                        listBoxItem.Classes.Add("menu-open");
                    }
                }
            }
        }

        private void MenuBase_OnClosed(object? sender, RoutedEventArgs e)
        {
            clearMenuOpenClasses();
        }

        private void clearMenuOpenClasses()
        {
            lock (__lockObj)
            {
                foreach (var listBoxItem in _attachedListBoxItems)
                {
                    listBoxItem.Classes.Remove("menu-open");
                }
                _attachedListBoxItems.Clear();
            }
        }
    }
}