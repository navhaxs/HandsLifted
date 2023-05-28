using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Utils
{
    internal static class FileUtils
    {
        internal static void DeleteDirectory(string path)
        {
            try
            {
                if (path != null && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                // LOG ME
            }
            finally { }
        }
    }
}
