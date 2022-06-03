using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.Render
{
    public class SlideState
        : ReactiveObject
    {
        public SlideState(Slide data)
        {
            Data = data;
        }

        public Slide Data { get; set; }
    }
}
