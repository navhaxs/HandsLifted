using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.PowerPointImporter
{
    public static class TempDir
    {
        public static string GetTempDirPath()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerSocketTemp");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }
    }
}
