using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using static HandsLiftedApp.Models.ItemState.ItemStateImpl;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using System.Collections;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.ItemExtensionState;
using Avalonia.Interactivity;
using ReactiveUI;
using System.Reactive;

namespace HandsLiftedApp.Views
{
    public partial class GroupItemsEditorWindow : Window
    {

        public Interaction<Unit, string?> ShowOpenFileDialog { get; }
        public Interaction<Unit, string?> ShowOpenFolderDialog { get; }

        //public ObservableCollection<Person> People { get; }
        public GroupItemsEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif


            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            //this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            //this.WhenActivated(d => d(ViewModel.ShowOpenFolderDialog.RegisterHandler(ShowOpenFolderDialog)));

            // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
            ShowOpenFileDialog = new Interaction<Unit, string?>();
            ShowOpenFolderDialog = new Interaction<Unit, string?>();

            this.DataContextChanged += GroupItemsEditorWindow_DataContextChanged;
        }
        private void GroupItemsEditorWindow_DataContextChanged(object? sender, System.EventArgs e)
        {
            //if (this.DataContext is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)
            //{
            //    this.FindControl<DataGrid>("DataGrid").Items = (IEnumerable)(((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)this.DataContext).Slides);
            //}
        }

        public void AddItemSingleClick(object? sender, RoutedEventArgs args)
        {

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
                //DataGrid_Items.Items = null;
                DataGrid_Items.Items = target.Items;
                DataGrid_Items.SelectedIndex = nextIndex ;
            }
        }
    }
}
