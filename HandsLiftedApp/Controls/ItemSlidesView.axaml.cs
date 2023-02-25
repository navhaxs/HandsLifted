using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System.Diagnostics;
using System.Windows;

namespace HandsLiftedApp.Controls
{
    public partial class ItemSlidesView : UserControl
    {
        public ItemSlidesView()
        {
            InitializeComponent();
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
            public Item<ItemStateImpl> SourceItem { get; set; }
        }
    }
}
