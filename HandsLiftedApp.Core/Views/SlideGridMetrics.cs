using System;
using System.Collections.Generic;

namespace HandsLiftedApp.Core.Views;

/// <summary>
/// Geometry shared by ItemSlidesView's slide grid and its empty-state placeholder.
/// ItemMargin must match the ListBoxItem Margin set in ItemSlidesView.axaml; PanelMarginX
/// must match the JustifiedWrapPanel Margin there.
/// </summary>
public static class SlideGridMetrics
{
    /// <summary>Margin around each ListBoxItem.</summary>
    public const double ItemMargin = 2;

    /// <summary>Label row: 14px text + 2px margin top/bottom of the template's bottom DockPanel.</summary>
    public const double LabelStripHeight = 24;

    public const double Spacing = 4;
    public const double PanelMarginX = 4;
    public const double Aspect = 16.0 / 9.0;

    public const double ExtraWidth = 2 * ItemMargin;
    public const double ExtraHeight = LabelStripHeight + 2 * ItemMargin;

    /// <summary>ListBox padding (8) + panel margin (8) + scrollbar allowance (16).</summary>
    public const double PanelInsetWidth = 8 + 8 + 16;

    private const double MinNotch = 10;

    /// <summary>
    /// Ascending notch multipliers (0-100 scale), one per column count 1..maxColumns. Each notch is the
    /// item width at which the panel (columns = (W + Spacing) / (itemWidth + Spacing)) fits exactly c
    /// columns. Callers pass the panel width (already minus PanelInsetWidth).
    /// </summary>
    public static double[] ColumnNotches(double availableWidth, double itemWidth, int maxColumns = 8)
    {
        var notches = new List<double>();
        for (int c = maxColumns; c >= 1; c--)
        {
            var width = (availableWidth + Spacing) / c - Spacing;
            // Nudge down by a tiny relative epsilon so floating-point rounding at the exact fit
            // can never push the panel's quotient just under c (which would give c-1 columns).
            var notch = width / itemWidth * 100 * (1 - 1e-9);
            if (notch < MinNotch) continue;
            if (notches.Count > 0 && Math.Abs(notches[^1] - notch) < 1e-9) continue;
            notches.Add(notch);
        }
        if (notches.Count == 0)
            notches.Add(Math.Max(MinNotch, availableWidth / itemWidth * 100 * (1 - 1e-9)));
        return notches.ToArray();
    }

    /// <summary>Adjacent notch in direction +1/-1 from value; the nearest end if none; value if no notches.</summary>
    public static double NextNotch(double[] notches, double value, int direction)
    {
        if (notches.Length == 0) return value;
        const double tol = 0.001;
        if (direction > 0)
        {
            foreach (var n in notches)
                if (n > value + tol) return n;
            return notches[^1];
        }
        for (int i = notches.Length - 1; i >= 0; i--)
            if (notches[i] < value - tol) return notches[i];
        return notches[0];
    }

    public static double NearestNotch(double[] notches, double value)
    {
        if (notches.Length == 0) return value;
        var best = notches[0];
        foreach (var n in notches)
            if (Math.Abs(n - value) < Math.Abs(best - value)) best = n;
        return best;
    }
}
