using LibMpv.Context.MVVM;

namespace Avalonia.Controls.LibMpv
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new MainWindowModel();
            InitializeComponent();
        }
    }
}