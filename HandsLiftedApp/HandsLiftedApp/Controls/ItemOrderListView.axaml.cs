using Avalonia;
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

        bool isMouseDown = false;

        public ItemOrderListView()
        {
            InitializeComponent();

            listBox = this.FindControl<ListBox>("itemsListBox");
            listBox.SelectionChanged += ListBox_SelectionChanged;
            listBox.PointerPressed += ListBox_PointerPressed;
            listBox.PointerReleased += ListBox_PointerReleased;

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

        private void ListBox_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            isMouseDown = true;
        }

        private void ListBox_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            isMouseDown = false;
        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (isMouseDown)
            {
                e.Handled = true;
                return;
            }

            MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = listBox.SelectedIndex });

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
