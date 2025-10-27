using Avalonia.Controls;
using Avalonia.Threading;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    public static class ImportWizard
    {
        public static async Task<String?> Run(Window owner)
        {
            var window = new ImportGoogleSlidesWindow();
            await Dispatcher.UIThread.InvokeAsync(async () => await window.ShowDialog(owner));
            return window.ImportId;
        }
    }
}