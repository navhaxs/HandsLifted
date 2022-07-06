using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Extensions
{
    // https://stackoverflow.com/a/6386234
    public static class TypeExtension
    {
        public static string GetNameWithoutGenericArity(this Type t)
        {
            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name.Substring(0, index);
        }
    }
}
