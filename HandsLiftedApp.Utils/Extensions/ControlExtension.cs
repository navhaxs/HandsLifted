using Avalonia.Controls;
using Avalonia.LogicalTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Extensions
{
    public static class ControlExtension
    {
        public static T FindAncestor<T>(this Control obj) where T : Control
        {
            var tmp = obj.Parent;
            while (tmp != null && !(tmp is T))
            {
                tmp = tmp.Parent;
            }
            return (T)tmp;
        }

        // TODO test me
        // TODO test me
        // TODO test me
        // TODO test me
        // TODO test me
        public static T? FindChild<T>(this Control obj) where T : Control
        {
            if (obj != null)
            {
                foreach (var child in obj.GetLogicalChildren())
                {
                    var res = FindChild<T>((Control)child);
                    if (res != null && (res is T))
                        return res;
                }
            }
            return (T)obj;
        }
    }
}
