using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Behaviours
{
    public class ItemsListBoxDropHandler : DropHandlerBase
    {
        private bool Validate<T>(ListBox listBox, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute) where T : Item<ItemStateImpl>
        {
            if (sourceContext is not T sourceItem
                || targetContext is not MainWindowViewModel vm
                || listBox.GetVisualAt(e.GetPosition(listBox)) is not IControl targetControl
                || targetControl.DataContext is not T targetItem)
            {
                return false;
            }

            var items = vm.Playlist.Items;
            var sourceIndex = items.IndexOf(sourceItem);
            var targetIndex = items.IndexOf(targetItem);

            if (sourceIndex < 0 || targetIndex < 0)
            {
                return false;
            }

            switch (e.DragEffects)
            {
                //case DragDropEffects.Copy:
                //    {
                //        if (bExecute)
                //        {
                //            //sourceItem.Clone
                //            var clone = new ItemViewModel() { Title = sourceItem.Title + "_copy" };
                //            InsertItem(items, clone, targetIndex + 1);
                //        }
                //        return true;
                //    }
                case DragDropEffects.Move:
                    {
                        if (bExecute)
                        {
                            bool shouldUpdateSelection = (listBox.SelectedIndex == sourceIndex);

                            MoveItem(items, sourceIndex, targetIndex);
                            //MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = targetIndex });
                            //listBox.SelectedIndex = targetIndex;

                            if (shouldUpdateSelection)
                            {
                                MessageBus.Current.SendMessage(new SpyScrollUpdateMessage() { Index = targetIndex });
                            }

                        }
                        return true;
                    }
                case DragDropEffects.Link:
                    {
                        if (bExecute)
                        {
                            bool shouldUpdateSelection = (listBox.SelectedIndex == sourceIndex);

                            SwapItem(items, sourceIndex, targetIndex);
                            //listBox.SelectedIndex = targetIndex;

                            if (shouldUpdateSelection)
                            {
                                MessageBus.Current.SendMessage(new SpyScrollUpdateMessage() { Index = targetIndex });
                            }

                        }
                        return true;
                    }
                default:
                    return false;
            }
        }

        public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            if (e.Source is IControl && sender is ListBox listBox)
            {
                return Validate<Item<ItemStateImpl>>(listBox, e, sourceContext, targetContext, false);
            }
            return false;
        }

        public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            if (e.Source is IControl && sender is ListBox listBox)
            {
                return Validate<Item<ItemStateImpl>>(listBox, e, sourceContext, targetContext, true);
            }
            return false;
        }
    }
}
