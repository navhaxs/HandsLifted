using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LibMpv.Client;
using Serilog;

namespace LibMpv.Thumbnailing;

public static class MpvThumbnailExtractor
{
    private static readonly TimeSpan FileLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SeekTimeout = TimeSpan.FromSeconds(5);

    public static async Task<WriteableBitmap?> ExtractAsync(
        string filePath,
        double seekFraction = 0.1,
        int maxWidth = 1280,
        int maxHeight = 720,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            return await Task.Run(
                    () => ExtractInternal(filePath, seekFraction, maxWidth, maxHeight, cts.Token),
                    cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[MpvThumbnailExtractor] Extraction timed out or cancelled for {Path}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MpvThumbnailExtractor] Extraction failed for {Path}", filePath);
            return null;
        }
    }

    private static WriteableBitmap? ExtractInternal(
        string filePath,
        double seekFraction,
        int maxWidth,
        int maxHeight,
        CancellationToken ct)
    {
        var ctx = new MpvContext();
        var frameSignal = new SemaphoreSlim(0);
        try
        {
            // Configure for headless thumbnail extraction.
            // Do NOT set vo=null — the SW render API replaces the VO and needs frame delivery.
            ctx.SetPropertyString("pause", "yes");
            ctx.SetPropertyString("aid", "no");     // skip audio decode — faster
            ctx.SetPropertyString("hr-seek", "no"); // keyframe-only seek — faster

            // StartSoftwareRendering internally calls StopRendering → Command("stop"), which fires
            // EndFile with reason=STOP. Create the render context FIRST so those spurious EndFile
            // events dispatch before we wire up fileLoadedTcs.
            // frameSignal fires each time mpv has a new frame ready to pull via SoftwareRender.
            ctx.StartSoftwareRendering(() =>
            {
                try { frameSignal.Release(); }
                catch { /* semaphore disposed */ }
            });

            // Register file-load events after SW render setup to avoid spurious pre-loadfile events.
            var fileLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.FileLoaded += (_, _) => fileLoadedTcs.TrySetResult(true);
            // Only cancel on actual error, not on stop/EOF caused by our own stop command.
            ctx.EndFile += (_, e) =>
            {
                if (e.Reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR)
                    fileLoadedTcs.TrySetResult(false);
            };

            ctx.Command("loadfile", filePath);

            // Wait for file to load (or fail)
            if (!fileLoadedTcs.Task.Wait((int)FileLoadTimeout.TotalMilliseconds, ct))
                return null; // timed out
            if (!fileLoadedTcs.Task.Result)
                return null; // EndFile fired with error before FileLoaded

            // Determine output dimensions from video dimensions
            int vidW = 0, vidH = 0;
            try
            {
                int.TryParse(ctx.GetPropertyString("width"), NumberStyles.Integer, CultureInfo.InvariantCulture, out vidW);
                int.TryParse(ctx.GetPropertyString("height"), NumberStyles.Integer, CultureInfo.InvariantCulture, out vidH);
            }
            catch { }

            if (vidW <= 0 || vidH <= 0)
                return null;

            double scale = Math.Min((double)maxWidth / vidW, (double)maxHeight / vidH);
            scale = Math.Min(1.0, scale);
            int outW = Math.Max(1, (int)Math.Round(vidW * scale));
            int outH = Math.Max(1, (int)Math.Round(vidH * scale));

            // Wait for first decodable frame (mpv fires UpdateCallback when frame ready)
            if (!frameSignal.Wait((int)FileLoadTimeout.TotalMilliseconds, ct))
                return null;

            var bitmap = RenderFrame(ctx, outW, outH);

            // Seek to target position for a better thumbnail frame
            if (seekFraction > 0)
            {
                double duration = 0;
                try
                {
                    double.TryParse(
                        ctx.GetPropertyString("duration"),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out duration);
                }
                catch { }

                if (duration > 1.0)
                {
                    double seekSec = Math.Clamp(duration * seekFraction, 0.0, duration - 0.1);
                    ctx.Command("seek", seekSec.ToString("F3", CultureInfo.InvariantCulture), "absolute");

                    // Drain any pre-seek frames already in the semaphore, then wait for the post-seek frame
                    while (frameSignal.CurrentCount > 0)
                        frameSignal.Wait(0);

                    if (frameSignal.Wait((int)SeekTimeout.TotalMilliseconds, ct))
                        bitmap = RenderFrame(ctx, outW, outH);
                    // else: seek timed out — keep the first-frame bitmap
                }
            }

            return bitmap;
        }
        finally
        {
            ctx.Dispose();
            frameSignal.Dispose();
        }
    }

    private static WriteableBitmap RenderFrame(MpvContext ctx, int outW, int outH)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(outW, outH),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var buf = bitmap.Lock();
        ctx.SoftwareRender(outW, outH, buf.Address, "bgra");

        return bitmap;
    }
}
