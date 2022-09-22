using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System.Diagnostics;
using System.Windows;

namespace HandsLiftedApp.Controls
{
    public partial class ItemStateView : UserControl
    {
        public ItemStateView()
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
            Debug.Print("OnSelectionChanged");
        }
    }
}
