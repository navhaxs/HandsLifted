using Avalonia.Animation.Easings;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public class XEasingOut : Easing
    {
        string _id;
        public XEasingOut()
        {
            _id = $"{DateTime.Now.GetHashCode()}";
        }

        /// <inheritdoc/>
        public override double Ease(double progress)
        {
            var next = Math.Sqrt(1 - Math.Pow(progress, 2));
            //System.Diagnostics.Debug.Print($"{_id}:{progress}={next}");
            return next;
        }
    }
}
