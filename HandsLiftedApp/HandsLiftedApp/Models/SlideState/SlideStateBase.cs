using ReactiveUI;

// unused
// unused
// unused
// unused
// unused
// unused
// unused
// unused

namespace HandsLiftedApp.Models.SlideState
{
    public class SlideStateBase<T>
        : ReactiveObject
    {

        public T _slide;

        public SlideStateBase(ref T slide)
        {
            _slide = slide;
        }

        // TODO add transition info 
        public int Index { get; set; }

        //public int SlideNumber { get => Index + 1; }

        //public SlideStateBase(Slide<ISlideState> data, int index)
        //{
        //    Data = data;
        //    Index = index;
        //}

        //public Slide<ISlideState> Data { get; set; }
    }
}
