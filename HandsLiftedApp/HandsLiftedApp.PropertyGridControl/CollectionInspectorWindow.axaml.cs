using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.PropertyGridControl
{
    public partial class CollectionInspectorWindow : Window
    {

        public CollectionInspectorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.DataContextChanged += ObjectInspectorWindow_DataContextChanged;
        }

        private void ObjectInspectorWindow_DataContextChanged(object? sender, EventArgs e)
        {
            this.Title = this.DataContext?.GetType().Name;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
