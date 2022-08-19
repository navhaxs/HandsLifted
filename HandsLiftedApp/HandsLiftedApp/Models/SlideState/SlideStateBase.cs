using ReactiveUI;

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
    }
}
