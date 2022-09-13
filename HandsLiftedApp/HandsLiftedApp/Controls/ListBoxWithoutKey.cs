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

        //public override void ScrollIntoView(object item)
        //{
        //    if (Items != null)
        //    {
        //        var index = Items.IndexOf(item);

        //        if (index != -1)
        //        {
        //            ScrollIntoView(index);
        //        }
        //    }
        //}
    }
}
