using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.Render
{
    public class SongSlideState : SlideState<SongSlide>
    {
        public SongSlideState(SongSlide data) : base(data)
        {
        }
    }
}
