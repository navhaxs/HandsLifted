using System.Globalization;
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
    private static readonly Dictionary<MpvContext, (WriteableBitmap? Bitmap, int RefCount)> SharedBitmaps = new();
    private static readonly object SharedLock = new object();
    private static SoftwareVideoView? PrimaryRenderer;
    
    private MpvContext? _mpvContext;
    private WriteableBitmap? _currentBitmap;
    private bool _isPrimaryRenderer;
    
    public static readonly StyledProperty<bool> ShowFpsProperty =
        AvaloniaProperty.Register<SoftwareVideoView, bool>(nameof(ShowFps));

    public bool ShowFps
    {
        get => GetValue(ShowFpsProperty);
        set => SetValue(ShowFpsProperty, value);
    }
    
    public MpvContext? MpvContext
    {
        get => _mpvContext;
        set
        {
            if (ReferenceEquals(value, _mpvContext)) return;

            if (_mpvContext != null)
            {
                UnregisterFromSharedBitmap();
                _mpvContext.StopRendering();
            }

            _mpvContext = value;

            if (_mpvContext != null)
            {
                RegisterForSharedBitmap();
                _mpvContext.StartSoftwareRendering(UpdateVideoView);
            }
        }
    }

    private void RegisterForSharedBitmap()
    {
        if (_mpvContext == null) return;
        
        lock (SharedLock)
        {
            if (!SharedBitmaps.ContainsKey(_mpvContext))
            {
                _isPrimaryRenderer = true;
                PrimaryRenderer = this;
            }
            
            if (!SharedBitmaps.TryGetValue(_mpvContext, out var entry))
            {
                SharedBitmaps[_mpvContext] = (null, 1);
            }
            else
            {
                SharedBitmaps[_mpvContext] = (entry.Bitmap, entry.RefCount + 1);
            }
        }
    }

    private void UnregisterFromSharedBitmap()
    {
        if (_mpvContext == null) return;
        
        lock (SharedLock)
        {
            if (SharedBitmaps.TryGetValue(_mpvContext, out var entry))
            {
                if (entry.RefCount <= 1)
                {
                    entry.Bitmap?.Dispose();
                    SharedBitmaps.Remove(_mpvContext);
                    if (_isPrimaryRenderer)
                    {
                        PrimaryRenderer = null;
                        _isPrimaryRenderer = false;
                    }
                }
                else
                {
                    SharedBitmaps[_mpvContext] = (entry.Bitmap, entry.RefCount - 1);
                }
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        if (VisualRoot == null || _mpvContext == null || !IsVisible) return;

        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;

        if (width <= 0 || height <= 0) return;

        lock (SharedLock)
        {
            if (_isPrimaryRenderer && _mpvContext != null)
            {
                // Only the primary renderer updates the bitmap
                if (SharedBitmaps.TryGetValue(_mpvContext, out var currentEntry))
                {
                    var bitmap = currentEntry.Bitmap;

                    // Check if the bitmap needs to be recreated
                    if (bitmap == null || bitmap.PixelSize.Width != width || bitmap.PixelSize.Height != height)
                    {
                        bitmap?.Dispose();
                        bitmap = new WriteableBitmap(
                            new PixelSize(width, height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul);
                        SharedBitmaps[_mpvContext] = (bitmap, currentEntry.RefCount);
                    }

                    using (var lockedBitmap = bitmap.Lock())
                    {
                        _mpvContext.SoftwareRender(width, height, lockedBitmap.Address, "bgra");
                    }

                    _currentBitmap = bitmap;
                }
            }
            else if (_mpvContext != null)
            {
                // Secondary renderers just use the shared bitmap
                _currentBitmap = SharedBitmaps[_mpvContext].Bitmap;
            }
        }

        if (_currentBitmap != null)
        {
            context.DrawImage(
                _currentBitmap,
                new Rect(0, 0, _currentBitmap.PixelSize.Width, _currentBitmap.PixelSize.Height),
                new Rect(Bounds.Size));
        }

        if (ShowFps)
        {
            DrawFps(context);
        }
    }

    private void UpdateVideoView()
    {
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (_mpvContext != null)
        {
            _mpvContext.StopRendering();
            UnregisterFromSharedBitmap();
        }
    }

    private DateTime _lastFpsUpdate = DateTime.Now;
    private int _frameCount = 0;
    private double _currentFps = 0;

    private void DrawFps(DrawingContext context)
    {
        _frameCount++;
        var now = DateTime.Now;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;

        // Update FPS calculation every second
        if (elapsed >= 1.0)
        {
            _currentFps = _frameCount / elapsed;
            _frameCount = 0;
            _lastFpsUpdate = now;
        }

        // Create the FPS text
        var text = new FormattedText(
            $"FPS: {_currentFps:F1}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.LightGreen);

        // Draw semi-transparent background
        var padding = 4;
        var background = new Rect(
            0,
            0,
            text.Width + (padding * 2),
            text.Height + (padding * 2));

        context.FillRectangle(
            new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            background);

        // Draw text
        context.DrawText(
            text,
            new Point(padding, padding));
    }

    public WriteableBitmap? GetVideoBufferBitmap()
    {
        return _currentBitmap;
    }
}