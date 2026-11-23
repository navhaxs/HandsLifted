using System;
using System.Collections.Generic;
using SkiaSharp;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Render.Skia.Builders;

/// <summary>
/// Turns any theme into a sample slide (<see cref="SlideRenderSpec"/>) using the real projector spec builders, so a theme
/// thumbnail looks exactly like the slides the theme produces. Song and general themes show sample lyrics, scripture
/// themes show a sample verse. A live motion video can't appear in a thumbnail, so the background is always the theme's
/// colour/image.
/// </summary>
public static class ThemePreviewSpecBuilder
{
    public const string SampleLyrics =
        "Shine Jesus shine\nFill this land\nWith the Father's glory\nBlaze Spirit blaze\nSet our hearts on fire";

    private const string SampleVerseText =
        "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life.";

    private const string SampleVerseHeader = "John 3:16";

    public static SlideRenderSpec Build(BaseSlideTheme? theme)
    {
        if (theme == null)
            return new SlideRenderSpec(new SolidBackground(SKColors.Black), Array.Empty<RenderElement>());

        var spec = theme.Type == SlideThemeType.ScriptureTheme
            ? BuildScripture(theme)
            : BuildSong(theme);

        // Colour/image only, whatever the slide instance resolved: the motion video itself is not drawn.
        return spec with { Background = ThemeBackgroundSpecBuilder.Build(theme, hasMotionBackground: false) };
    }

    private static SlideRenderSpec BuildSong(BaseSlideTheme theme)
    {
        var slide = new SongSlideInstance(null, null, "preview", SampleLyrics) { Theme = theme };
        return SongSlideSpecBuilder.Build(slide);
    }

    private static SlideRenderSpec BuildScripture(BaseSlideTheme theme)
    {
        var verses = new List<ScriptureVerseRef> { new(3, 16, SampleVerseText) };
        var pages = ScriptureParagraphLayoutEngine.Paginate(verses, SampleVerseHeader, theme);

        var slide = new ScriptureSlideInstance(null, "preview") { Theme = theme };
        if (pages.Count > 0)
        {
            slide.Lines = pages[0].Lines;
            slide.EffectiveFontSize = pages[0].FontSize;
        }

        return ScriptureParagraphSpecBuilder.Build(slide);
    }
}
