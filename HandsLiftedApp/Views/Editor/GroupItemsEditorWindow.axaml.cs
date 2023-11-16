using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views
{
    public partial class GroupItemsEditorWindow : ReactiveWindow<GroupItemsEditorViewModel>
    {
        public GroupItemsEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            if (Design.IsDesignMode)
                return;

            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            this.DataContextChanged += GroupItemsEditorWindow_DataContextChanged;
        }

        private async Task ShowOpenFileDialog(InteractionContext<Unit, string[]?> interaction)
        {
            var dialog = new OpenFileDialog() { AllowMultiple = true }; // TODO Pass as flag
            var fileNames = await dialog.ShowAsync(this);
            interaction.SetOutput(fileNames);
        }

        private void GroupItemsEditorWindow_DataContextChanged(object? sender, System.EventArgs e)
        {
            //this.FindControl<DataGrid>("DataGrid").ItemsSource = ViewModel.Item.Slides;
        }

        public void AddItemFolderClick(object? sender, RoutedEventArgs args)
        {

        }
        public void MoveItemUpClick(object? sender, RoutedEventArgs args)
        {
            if (DataGrid_Items.SelectedIndex > -1)
            {
                updateSelectedIndex(DataGrid_Items.SelectedIndex, DataGrid_Items.SelectedIndex - 1);
            }
        }
        public void MoveItemDownClick(object? sender, RoutedEventArgs args)
        {
            if (DataGrid_Items.SelectedIndex > -1)
            {
                updateSelectedIndex(DataGrid_Items.SelectedIndex, DataGrid_Items.SelectedIndex + 1);
            }
        }

        void updateSelectedIndex(int fromIndex, int nextIndex)
        {
            SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> target = (SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)this.DataContext;

            if (target == null)
                return;

            nextIndex = (nextIndex + target.Items.Count) % target.Items.Count;

            if (nextIndex > -1 &&
                nextIndex < target.Items.Count)
            {
                target.Items.Move(fromIndex, nextIndex);
                DataGrid_Items.ItemsSource = null;
                DataGrid_Items.ItemsSource = target.Items;
                DataGrid_Items.SelectedIndex = nextIndex;
            }
        }
    }
}
