using Avalonia.Animation.Easings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.XTransitioningContentControl
{
    public class XSmoothStep : Easing
    {
        string _id;
        double min = 0d;
        double max = 1d;
        public XSmoothStep()
        {
            _id = $"{DateTime.Now.GetHashCode()}";
        }

        /// <inheritdoc/>
        public override double Ease(double value)
        {
            var x = Math.Max(0, Math.Min(1, (value - min) / (max - min)));
            var next = x * x * (3 - 2 * x);

            //System.Diagnostics.Debug.Print($"{_id}:{value}={next}");
            return next;
        }
    }
}
