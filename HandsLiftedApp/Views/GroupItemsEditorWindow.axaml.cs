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

namespace HandsLiftedApp.Views
{
    public partial class GroupItemsEditorWindow : Window
    {
        public ObservableCollection<Person> People { get; }
        public GroupItemsEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.DataContextChanged += GroupItemsEditorWindow_DataContextChanged;

        }

        private void GroupItemsEditorWindow_DataContextChanged(object? sender, System.EventArgs e)
        {
            if (this.DataContext is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)
            {
                this.FindControl<DataGrid>("DataGrid").Items = (IEnumerable)(((SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>)this.DataContext).Slides);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }
    }
}
