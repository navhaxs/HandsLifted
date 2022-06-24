using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DynamicData.Binding;
using HandsLiftedApp.PropertyGridControl;
using System.Collections.ObjectModel;

namespace HandsLiftedApp.PropertyGridControl
{
    public partial class CollectionInspectorWindow : Window
    {

        private ListBox _listBox;
        private PropertyGrid _propertyGrid;

        public class Hello
        {
            public string World { get; set; } = "pizza!";
        }
        public CollectionInspectorWindow()
        {
            InitializeComponent();
//#if DEBUG
//            this.AttachDevTools();
//#endif

            _propertyGrid = this.Find<PropertyGrid>("propertyGrid");
            _propertyGrid.SelectedObject = new Hello();
            _listBox = this.Find<ListBox>("listBox");

            this.DataContextChanged += ObjectInspectorWindow_DataContextChanged;
        }

        private void ObjectInspectorWindow_DataContextChanged(object? sender, EventArgs e)
        {
            _propertyGrid.SelectedObject = this.DataContext;
            this.Title = this.DataContext?.GetType().Name;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
