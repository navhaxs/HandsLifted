using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Slides;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Utils
{
    public class MyTemplateSelector : IDataTemplate
    {
        public bool SupportsRecycling => false;
        [Content]
        public Dictionary<string, IDataTemplate> Templates { get; } = new Dictionary<string, IDataTemplate>();

        public IControl Build(object data)
        {
            if (data is SongSlide)
            {
                System.Diagnostics.Debug.Print("SongSlide");
                return Templates["SongSlide"].Build(data);
            }
            else if (data is ImageSlide)
            {
                return Templates["ImageSlide"].Build(data);
            }

            System.Diagnostics.Debug.Print("MyKey2");
            //return Templates[((SongSlideViewModel)data).Text].Build(data);
            return Templates["MyKey2"].Build(data);
        }

        public bool Match(object data)
        {
            return data is Slide;
        }
    }
}
