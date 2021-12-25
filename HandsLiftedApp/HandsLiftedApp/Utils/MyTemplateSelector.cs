using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using HandsLiftedApp.Models;
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
                return Templates["SongSlide"].Build(data);
            }
            else if (data is ImageSlide)
            {
                return Templates["ImageSlide"].Build(data);
            }
            //return Templates[((SongSlideViewModel)data).Text].Build(data);
            return Templates["MyKey2"].Build(data);
        }

        public bool Match(object data)
        {
            return data is Slide;
        }
    }
}
