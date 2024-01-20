using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls.Messages;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Controls.Assets
{
    class AddItemFlyoutResourceDictionary : ResourceDictionary
    {
        public void OnMenuItemClick(object? sender, RoutedEventArgs args)
        {
            if (sender is MenuItem menuItem)
            {
                AddItemMessage.AddItemType type;
                if (menuItem.CommandParameter != null)
                {
                    Enum.TryParse(menuItem.CommandParameter.ToString(), out type);
                    MessageBus.Current.SendMessage(new AddItemMessage { Type = type });
                }
                else
                {
                    MessageBus.Current.SendMessage(new AddItemMessage());
                }
            }

        }
    }
}
