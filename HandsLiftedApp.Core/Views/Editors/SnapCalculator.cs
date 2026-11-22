using System;
using System.Collections.Generic;
using Avalonia;

namespace HandsLiftedApp.Core.Views.Editors
{
    public record SnapResult(Rect Rect, double? GuideX, double? GuideY);

    /// <summary>
    /// Snaps a dragged rectangle's edges/centre to the slide's edges/centre and to other elements' edges/centre.
    /// </summary>
    public static class SnapCalculator
    {
        public static SnapResult Snap(Rect dragged, IReadOnlyList<Rect> others, double slideWidth, double slideHeight,
            double threshold = 10)
        {
            var dx = BestOffset(
                new[] { dragged.Left, dragged.Left + dragged.Width / 2, dragged.Right },
                CandidatesX(others, slideWidth), threshold, out var guideX);
            var dy = BestOffset(
                new[] { dragged.Top, dragged.Top + dragged.Height / 2, dragged.Bottom },
                CandidatesY(others, slideHeight), threshold, out var guideY);

            var rect = new Rect(dragged.X + dx, dragged.Y + dy, dragged.Width, dragged.Height);
            return new SnapResult(rect, guideX, guideY);
        }

        private static IEnumerable<double> CandidatesX(IReadOnlyList<Rect> others, double slideWidth)
        {
            yield return 0;
            yield return slideWidth / 2;
            yield return slideWidth;
            foreach (var o in others)
            {
                yield return o.Left;
                yield return o.Left + o.Width / 2;
                yield return o.Right;
            }
        }

        private static IEnumerable<double> CandidatesY(IReadOnlyList<Rect> others, double slideHeight)
        {
            yield return 0;
            yield return slideHeight / 2;
            yield return slideHeight;
            foreach (var o in others)
            {
                yield return o.Top;
                yield return o.Top + o.Height / 2;
                yield return o.Bottom;
            }
        }

        // Smallest |candidate - edge| within the threshold wins; returns the shift to apply and the guide coordinate.
        private static double BestOffset(double[] edges, IEnumerable<double> candidates, double threshold,
            out double? guide)
        {
            guide = null;
            var best = double.MaxValue;
            var offset = 0.0;
            foreach (var candidate in candidates)
            foreach (var edge in edges)
            {
                var delta = candidate - edge;
                var distance = Math.Abs(delta);
                if (distance <= threshold && distance < best)
                {
                    best = distance;
                    offset = delta;
                    guide = candidate;
                }
            }
            return offset;
        }
    }
}

namespace HandsLiftedApp.Core.Views.Editors
{
    /// <summary>
    /// One drag gesture. Accumulates the raw (unsnapped) pointer movement from the start rect and snaps that,
    /// so the snapped result never feeds back into the next move and the rect can always leave a snap point.
    /// </summary>
    public sealed class DragSnapSession
    {
        private Rect _raw;

        public DragSnapSession(Rect start) => _raw = start;

        public SnapResult Move(double deltaX, double deltaY, IReadOnlyList<Rect> others, double slideWidth,
            double slideHeight, double threshold = 10)
        {
            _raw = new Rect(_raw.X + deltaX, _raw.Y + deltaY, _raw.Width, _raw.Height);
            return SnapCalculator.Snap(_raw, others, slideWidth, slideHeight, threshold);
        }
    }
}
