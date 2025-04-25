using System.Globalization;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaNDI;
using LibMpv.Client;

namespace Avalonia.Controls.LibMpv;

public class SoftwareVideoView : Control, IGetVideoBufferBitmap
{
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _mpvContext?.UnregisterUpdateCallback(this.UpdateVideoView);
    }

    WriteableBitmap renderTarget;

    private MpvContext? _mpvContext = null;

    public static readonly DirectProperty<SoftwareVideoView, MpvContext?> MpvContextProperty =
        AvaloniaProperty.RegisterDirect<SoftwareVideoView, MpvContext?>(
            nameof(MpvContext),
            o => o.MpvContext,
            (o, v) => o.MpvContext = v,
            defaultBindingMode: BindingMode.TwoWay);

    public MpvContext? MpvContext
    {
        get { return _mpvContext; }
        set
        {
            if (ReferenceEquals(value, _mpvContext)) return;
            _mpvContext?.StopRendering();
            _mpvContext = value;
            _mpvContext?.StartSoftwareRendering(this.UpdateVideoView);
        }
    }

    public static readonly StyledProperty<bool> ShowFpsProperty =
        AvaloniaProperty.Register<SoftwareVideoView, bool>(nameof(ShowFps));

    public bool ShowFps
    {
        get => GetValue(ShowFpsProperty);
        set => SetValue(ShowFpsProperty, value);
    }

    public WriteableBitmap GetVideoBufferBitmap() => renderTarget;

    private DateTime _lastRenderTime = DateTime.Now;
    private double _currentFps = 0;
    private FormattedText _fpsText;

    public SoftwareVideoView()
    {
        ClipToBounds = true;
        _fpsText = new FormattedText(
            "FPS: 0",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.Lime);
    }

    public override void Render(DrawingContext context) // what calls this?
    {
        if (VisualRoot == null || _mpvContext == null)
            return;

        var bitmapSize = GetPixelSize();

        if (bitmapSize.Height == 0 || bitmapSize.Width == 0)
            return;

        if (renderTarget == null || renderTarget.PixelSize.Width != bitmapSize.Width ||
            renderTarget.PixelSize.Height != bitmapSize.Height)
            this.renderTarget = new WriteableBitmap(bitmapSize, new Vector(96.0, 96.0), PixelFormat.Bgra8888,
                AlphaFormat.Premul);

        using (ILockedFramebuffer lockedBitmap = this.renderTarget.Lock())
        {
            _mpvContext.SoftwareRender(lockedBitmap.Size.Width, lockedBitmap.Size.Height, lockedBitmap.Address, "bgra");
        }

        // TODO can this renderTarget be sent to NDI?
        context.DrawImage(this.renderTarget,
            new Rect(0, 0, renderTarget.PixelSize.Width, renderTarget.PixelSize.Height));

        // Calculate and display FPS
        var currentTime = DateTime.Now;

        if (ShowFps)
        {
            var frameTime = (currentTime - _lastRenderTime).TotalSeconds;
            if (frameTime > 0)
            {
                _currentFps = 0.95 * _currentFps + 0.05 * (1.0 / frameTime); // Smooth FPS
                _fpsText = new FormattedText(
                    $"FPS: {_currentFps:F1}",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.Lime);
            }
        }

        _lastRenderTime = currentTime;

        // Draw FPS counter in the top-left corner
        if (ShowFps)
            context.DrawText(_fpsText, new Point(10, 10));
    }

    // TODO: how can this scale down for smaller preview outputs to increase overall performance when multiple outputs active?
    private PixelSize GetPixelSize()
    {
        var scaling = VisualRoot!.RenderScaling;
        //return new PixelSize(Math.Max(1, (int)(Bounds.Width * scaling)),Math.Max(1, (int)(Bounds.Height * scaling)));
        return new PixelSize((int)Bounds.Width, (int)Bounds.Height);
    }

    private void UpdateVideoView()
    {
        Dispatcher.UIThread.Post(this.InvalidateVisual, DispatcherPriority.Background);
    }
}