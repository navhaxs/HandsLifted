using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Core.Assets
{
    class AddItemFlyoutResourceDictionary : ResourceDictionary
    {
        public void OnMenuItemClick(object? sender, RoutedEventArgs args)
        {
            if (sender is MenuItem menuItem)
            {
                Item? nearestItem = null;
                int? itemInsertIndex = null;
                
                var parentAddItemButton = ControlExtension.FindAncestor<AddItemButton>(menuItem);

                if (parentAddItemButton != null && parentAddItemButton.ItemInsertIndex != null)
                {
                    itemInsertIndex = parentAddItemButton.ItemInsertIndex;
                }
                else
                {
                    var nearestDataContext = FindNearestDataContextAncestor(menuItem);
                    if (nearestDataContext is Item item)
                    {
                        nearestItem = item;
                    }
                }

                AddItemMessage.AddItemType type;
                if (menuItem.CommandParameter != null)
                {
                    Enum.TryParse(menuItem.CommandParameter.ToString(), out type);
                    MessageBus.Current.SendMessage(new AddItemMessage { Type = type, ItemToInsertAfter = nearestItem, InsertIndex = itemInsertIndex });
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

        }
        
        /// <summary>
        /// Finds first ancestor of given type.
        /// </summary>
        /// <typeparam name="T">Ancestor type.</typeparam>
        /// <param name="visual">The visual.</param>
        /// <param name="includeSelf">If given visual should be included in search.</param>
        /// <returns>First ancestor of given type.</returns>
        public Object? FindNearestDataContextAncestor(Control? control)
        {
            if (control is null)
            {
                return null;
            }

            Control? it = control;

            while (it != null)
            {
                if (it.DataContext != null)
                {
                    return it.DataContext;
                }

                if (it.Parent is Control parent)
                {
                    it = parent;
                }
                else
                {
                    it = null;
                }
            }

            return null;
        }
    }
}
