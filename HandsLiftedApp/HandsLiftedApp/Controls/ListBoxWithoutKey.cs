using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Controls
{
    internal class ListBoxWithoutKey : ListBox
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // do nothing instead of the default behaviour!
        }
    }
}
