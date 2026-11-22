// HandsLiftedApp.Core/Render/Skia/Builders/CustomSlideSpecBuilder.cs
using System;
using Serilog;
using SkiaSharp;
using CustomSlideModel = HandsLiftedApp.Data.Data.Models.Slides.CustomSlide;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

// Native SkiaSharp renderer for CustomSlide (element-based).
// Avoids off-screen Avalonia control rendering, which requires an active visual tree.
public static class CustomSlideSpecBuilder
{
    private const int CanvasWidth = CustomSlideDrawer.CanvasWidth;
    private const int CanvasHeight = CustomSlideDrawer.CanvasHeight;

    public static SlideRenderSpec? Build(CustomSlideModel slide)
    {
        try
        {
            var info = new SKImageInfo(CanvasWidth, CanvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null)
            {
                Log.Warning("[CustomSlideSpecBuilder] SKSurface.Create failed");
                return null;
            }

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            CustomSlideDrawer.Draw(canvas, slide);

            using var skImage = surface.Snapshot();
            var bitmap = SKBitmap.FromImage(skImage);
            return new SlideRenderSpec(new SkiaBitmapBackground(bitmap), Array.Empty<RenderElement>());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CustomSlideSpecBuilder] Failed to render CustomSlide");
            return null;
        }
    }
}
