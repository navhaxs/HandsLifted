using Avalonia.Controls;

namespace ScratchApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            HandsLiftedApp.Importer.GoogleSlides.Program x = new HandsLiftedApp.Importer.GoogleSlides.Program();
            HandsLiftedApp.Importer.GoogleSlides.Program.Main(new string[0]);
        }
    }
}
