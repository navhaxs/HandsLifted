using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.IO;
using System.Text;

namespace HandsLiftedApp.Utils.Render
{
    internal static class XamlLoader
    {
        public static UserControl LoadFromXaml(string xamlFilePath)
        {
            string readContents;
            using (StreamReader streamReader = new StreamReader(xamlFilePath, Encoding.UTF8))
            {
                readContents = streamReader.ReadToEnd();
            }
            return AvaloniaRuntimeXamlLoader.Parse<UserControl>(readContents);
        }
    }
}
