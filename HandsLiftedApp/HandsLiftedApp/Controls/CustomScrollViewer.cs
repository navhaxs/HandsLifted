using Avalonia.Controls;
using Avalonia.Input;

namespace HandsLiftedApp.Controls
{
    public class CustomScrollViewer : ScrollViewer
    {
        public CustomScrollViewer() : base()
        {

        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }
    }
}
