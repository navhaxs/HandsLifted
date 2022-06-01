using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.Render;
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

            var dataType = data.GetType().Name;

            if (Templates.ContainsKey(dataType))
            {
                return Templates[dataType].Build(data);
            }

            //if (data is SongSlide)
            //{
            //    System.Diagnostics.Debug.Print("SongSlide");
            //    return Templates["SongSlide"].Build(data);
            //}
            //else if (data is ImageSlide)
            //{
            //    return Templates["ImageSlide"].Build(data);
            //}

            //else if (data is VideoSlide && Templates.ContainsKey("VideoSlide"))
            //{
            //    return Templates["VideoSlide"].Build(data);
            //}

            //return Templates[((SongSlideViewModel)data).Text].Build(data);
            else if (Templates.ContainsKey("Fallback"))
            {
                return Templates["Fallback"].Build(data);
            }
            return null;
        }

        public bool Match(object data)
        {
            //return data is SlideState;
            return true;
        }
    }
}
