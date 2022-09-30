using Avalonia.Animation.Easings;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public class XEaseInOutExpo : Easing
    {
        string _id;
        public XEaseInOutExpo()
        {
            _id = $"{DateTime.Now.GetHashCode()}";
        }

        /// <inheritdoc/>
        public override double Ease(double x)
        {
            return x == 0
              ? 0
              : x == 1
              ? 1
              : x < 0.5 ? Math.Pow(2, 20 * x - 10) / 2
              : (2 - Math.Pow(2, -20 * x + 10)) / 2;
        }
    }
}
