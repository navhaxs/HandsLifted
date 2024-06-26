﻿using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaNDI;
using LibMpv.Client;

namespace Avalonia.Controls.LibMpv;

public class SoftwareVideoView : Control, IGetVideoBufferBitmap
{
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

    public WriteableBitmap GetVideoBufferBitmap() => renderTarget;

    public SoftwareVideoView()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context) // what calls this?
    {
        if (VisualRoot == null || _mpvContext == null)
            return;

        var bitmapSize = GetPixelSize();

        if (bitmapSize.Height == 0 || bitmapSize.Width == 0)
            return;

        if (renderTarget == null || renderTarget.PixelSize.Width != bitmapSize.Width || renderTarget.PixelSize.Height != bitmapSize.Height)
            this.renderTarget = new WriteableBitmap(bitmapSize, new Vector(96.0, 96.0), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (ILockedFramebuffer lockedBitmap = this.renderTarget.Lock())
        {
            _mpvContext.SoftwareRender(lockedBitmap.Size.Width, lockedBitmap.Size.Height, lockedBitmap.Address, "bgra");
        }

        // TODO can this renderTarget be sent to NDI?
        context.DrawImage(this.renderTarget, new Rect(0, 0, renderTarget.PixelSize.Width, renderTarget.PixelSize.Height));
    }

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
