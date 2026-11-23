using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Tests.TestSupport;
using ReactiveUI;
using SkiaSharp;

namespace HandsLiftedApp.Tests.Render.Skia;

[TestClass]
public class ThemePreviewSpecBuilderTests
{
    private static BaseSlideTheme MakeTheme(SlideThemeType type) => new BaseSlideTheme
    {
        Type = type,
        FontSize = 60,
        BackgroundColour = Colors.Red,
        TextColour = Colors.White,
    };

    [TestMethod]
    public Task Build_SongTheme_HasTextElementsAndThemeColourBackground() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.SongTheme));

        Assert.IsTrue(spec.Elements.Count > 0);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
        Assert.AreEqual(new SKColor(255, 0, 0, 255), ((SolidBackground)spec.Background).Color);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_GeneralTheme_UsesSongLyrics() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.General));

        Assert.IsTrue(spec.Elements.Count > 0);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_ScriptureTheme_HasTextElements() => DispatcherTestThread.Run(() =>
    {
        var spec = ThemePreviewSpecBuilder.Build(MakeTheme(SlideThemeType.ScriptureTheme));

        Assert.IsTrue(spec.Elements.Count > 0);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Build_ThemeWithMotionVideo_BackgroundIsNotTransparent() => DispatcherTestThread.Run(() =>
    {
        var theme = MakeTheme(SlideThemeType.SongTheme);
        theme.DefaultMotionBackgroundVideoPath = "x.mp4";
        theme.BackgroundOpacity = 100;

        var spec = ThemePreviewSpecBuilder.Build(theme);

        Assert.IsNotInstanceOfType(spec.Background, typeof(TransparentBackground));
        return Task.CompletedTask;
    });

    /// <summary>Number of handlers currently attached to the object's INotifyPropertyChanged.PropertyChanged event.</summary>
    /// <remarks>Reflects on ReactiveUI's <c>ReactiveObject.PropertyChangedHandler</c> field: expect to adjust it on a ReactiveUI upgrade.</remarks>
    private static int PropertyChangedHandlerCount(object target)
    {
        var field = typeof(ReactiveObject).GetField("PropertyChangedHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?? throw new AssertFailedException("ReactiveObject no longer has a PropertyChangedHandler field to count handlers on");
        return (field.GetValue(target) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }

    // Building a preview with a throwaway slide instance subscribed that slide to the theme for the life of the theme
    // (every later edit of the theme then queued a full-HD render per leaked slide). The builder must leave the theme
    // exactly as it found it.
    [TestMethod]
    [DataRow(SlideThemeType.SongTheme)]
    [DataRow(SlideThemeType.General)]
    [DataRow(SlideThemeType.ScriptureTheme)]
    public Task Build_DoesNotSubscribeAnythingToTheTheme(SlideThemeType type) => DispatcherTestThread.Run(() =>
    {
        var theme = MakeTheme(type);
        var before = PropertyChangedHandlerCount(theme);

        for (var i = 0; i < 5; i++)
            ThemePreviewSpecBuilder.Build(theme);

        Assert.AreEqual(before, PropertyChangedHandlerCount(theme), "Build left subscriptions on the theme");
        return Task.CompletedTask;
    });

    [TestMethod]
    public void Build_NullTheme_ReturnsBlackSolidBackgroundNoElements()
    {
        var spec = ThemePreviewSpecBuilder.Build(null);

        Assert.AreEqual(0, spec.Elements.Count);
        Assert.IsInstanceOfType(spec.Background, typeof(SolidBackground));
        Assert.AreEqual(SKColors.Black, ((SolidBackground)spec.Background).Color);
    }
}
