using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class SlideStateBase
        : ReactiveObject
    {

        public int Index { get; set; }

        public int SlideNumber { get => Index + 1; }

        public SlideStateBase(Slide data, int index)
        {
            Data = data;
            Index = index;
        }

        public Slide Data { get; set; }
    }
}
