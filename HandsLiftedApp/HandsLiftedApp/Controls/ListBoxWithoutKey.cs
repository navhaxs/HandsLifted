using Avalonia.Controls;
using Avalonia.Input;

namespace HandsLiftedApp.Controls
{
    public class ListBoxWithoutKey : ListBox
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }

        // TODO: bug
    }
}
