// HandsLiftedApp.Core/Render/Skia/Builders/SlideSpecResolver.cs
using System;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

/// <summary>
/// Single shared copy of the "Slide -> SlideRenderSpec" switch. Previously duplicated
/// independently in LivePane.axaml.cs, ProjectorWindow.axaml.cs and DefaultLayout.axaml.cs —
/// kept here so callers that only need a spec (not a view) don't need their own copy, and so
/// future slide types only need to be added in one place.
/// </summary>
public static class SlideSpecResolver
{
    public static SlideRenderSpec? Resolve(Slide? slide, string? logoPath) => slide switch
    {
        SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
        SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
        ScriptureSlideInstance sc => ScriptureParagraphSpecBuilder.Build(sc),
        ImageSlideInstance img   => IsValidMediaPath(img.SourceMediaFilePath)
            ? new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>())
            : null,
        LogoSlide                => IsValidMediaPath(logoPath)
            ? new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>())
            : null,
        HandsLiftedApp.Data.Data.Models.Slides.CustomSlide cs => CustomSlideSpecBuilder.Build(cs),
        _                        => null,
    };

    /// <summary>
    /// Repairs paths where the serializer has mangled an avares:// URI into a Windows-style
    /// absolute path (e.g. "C:\...\avares:\Assembly\Assets\...").
    /// Returns the corrected avares:// URI, or the original path unchanged for normal files.
    /// </summary>
    public static string? NormalizeMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var idx = path.IndexOf("avares:", StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
        {
            var rest = path.Substring(idx + "avares:".Length)
                           .Replace('\\', '/')
                           .TrimStart('/');
            if (rest.Length == 0) return path; // malformed — return original unchanged
            return "avares://" + rest;
        }

        return path;
    }

    // Returns true when a media path is expected to resolve to real content.
    // avares:// URIs are always treated as valid (checked at render time).
    // File system paths are only valid when the file actually exists, so that
    // a missing file produces a null spec (smooth fade to black) rather than
    // an ImageBackground spec whose bitmap silently fails to load mid-transition.
    public static bool IsValidMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)) return true;
        return System.IO.File.Exists(path);
    }
}
