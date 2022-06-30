using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.PropertyGridControl
{
    public partial class ObjectInspectorWindow : Window
    {

        private PropertyGrid _propertyGrid;
        public ObjectInspectorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            _propertyGrid = this.Find<PropertyGrid>("propertyGrid");

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
