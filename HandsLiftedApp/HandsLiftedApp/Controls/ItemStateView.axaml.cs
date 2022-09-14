using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
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
            MessageBus.Current.SendMessage(new FocusSelectedItem());
        }
    }
}
