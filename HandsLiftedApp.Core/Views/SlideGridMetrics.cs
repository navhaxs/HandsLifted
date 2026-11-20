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
}
