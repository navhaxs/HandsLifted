using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideState
{
    public class SongSlideState : SlideStateBase
    {
        public SongSlideState(SongSlide data, int index) : base(data, index)
        {
        }
    }
}
