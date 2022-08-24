using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Extensions
{
    public static class IControlExtension
    {
        public static T FindAncestor<T>(this IControl obj) where T : IControl
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
        public static T? FindChild<T>(this IControl obj) where T : IControl
        {
            if (obj != null)
            {
                foreach (var child in obj.LogicalChildren)
                {
                    var res = FindChild<T>((IControl)child);
                    if (res != null && (res is T))
                        return res;
                }
            }
            return (T)obj;
        }
    }
}
