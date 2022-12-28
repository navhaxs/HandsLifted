using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace HandsLiftedApp.Controls
{
    public partial class ItemOrderListView : UserControl
    {
        private static readonly object syncSlidesLock = new object();

        ListBox listBox;

        public ItemOrderListView()
        {
            InitializeComponent();

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

        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (listBox.SelectedIndex > -1)
                MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = listBox.SelectedIndex });

        }
    }
}
