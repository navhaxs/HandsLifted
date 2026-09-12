// HandsLiftedApp.Core/Services/SlidePreloadService.cs
using System;
using System.Reactive.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Services;

/// <summary>
/// Predictively warms the shared image bitmap cache (<see cref="SlideRenderer"/>'s cache) for
/// the upcoming slide once slide navigation has been idle for a bit, so actually navigating to
/// it later is a cache hit instead of a fresh decode.
///
/// Lives at the playlist level rather than in any one window's code-behind: the bitmap cache is
/// shared across LivePane, ProjectorWindow and StageDisplayWindow, and there is exactly one
/// PlaylistInstance for the whole app (Globals.Instance.MainViewModel.Playlist), so a single
/// app-lifetime subscription here covers every window — no per-window duplication, and it keeps
/// working even if no window happens to be open.
/// </summary>
public static class SlidePreloadService
{
    private static IDisposable? _subscription;

    public static void Initialize(PlaylistInstance playlist)
    {
        _subscription?.Dispose();
        _subscription = playlist
            .WhenAnyValue(p => p.NextSlide)
            .Throttle(TimeSpan.FromMilliseconds(400), RxSchedulers.TaskpoolScheduler)
            .Subscribe(nextSlide => Preload(nextSlide, playlist));
    }

    private static void Preload(Slide? nextSlide, PlaylistInstance playlist)
    {
        var logoPath = SlideSpecResolver.NormalizeMediaPath(playlist.LogoGraphicFile);
        var spec = SlideSpecResolver.Resolve(nextSlide, logoPath);
        if (spec?.Background is ImageBackground)
            SlideRenderer.Preload(spec);
    }
}
