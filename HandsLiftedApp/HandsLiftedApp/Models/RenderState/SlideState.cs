using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.Render
{
    public class SlideState<T> : ReactiveObject where T : Slide
    {
        public SlideState(T data)
        {
            Data = data;
        }

        T Data { get; set; }
    }
}
