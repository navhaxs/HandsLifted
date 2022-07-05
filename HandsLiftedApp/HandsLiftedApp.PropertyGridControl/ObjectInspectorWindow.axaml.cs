using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.PropertyGridControl
{
    public partial class ObjectInspectorWindow : Window
    {

        private Button _debugButton;
        private PropertyGrid _propertyGrid;
        public ObjectInspectorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            _debugButton = this.Find<Button>("debugButton");
            _debugButton.Click += _debugButton_Click;

            _propertyGrid = this.Find<PropertyGrid>("propertyGrid");

            this.DataContextChanged += ObjectInspectorWindow_DataContextChanged;
        }

        private void _debugButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                var inspectMe = this.DataContext;
                System.Diagnostics.Debugger.Break();
            }
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
