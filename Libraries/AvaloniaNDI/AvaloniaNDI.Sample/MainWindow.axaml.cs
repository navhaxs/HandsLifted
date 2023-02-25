using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvaloniaNDI.Sample
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public bool IsContentHighResCheckFunc(NDISendContainer sendContainer)
        {
            /* e.g.
             
            if (((MainWindowViewModel)this.DataContext).ActiveSlide is VideoView)
                return true;

            return false;

            */

            return true;
        }
    }
}
