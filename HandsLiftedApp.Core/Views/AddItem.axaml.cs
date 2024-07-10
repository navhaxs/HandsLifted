using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class AddItem : UserControl
    {
        public AddItem()
        {
            InitializeComponent();

            GotFocus += delegate { SearchBox.Focus(); };
        }

        private void ImportFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AddItemViewModel vm)
            {
                RunImport(vm);
            }
        }

        private async void RunImport(AddItemViewModel vm)
        {
            var filePaths = await vm.ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
            if (filePaths != null)
            {
                MessageBus.Current.SendMessage(new AddItemToPlaylistMessage(new List<string>(filePaths)));
            }
        }
    }
}