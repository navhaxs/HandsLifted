using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class IsDefaultThemeConvertersTests
{
    private static IEnumerable<IMultiValueConverter> All()
    {
        yield return new IsDefaultSongThemeConverter();
        yield return new IsDefaultSongMotionThemeConverter();
        yield return new IsDefaultScriptureThemeConverter();
    }

    private static object? Run(IMultiValueConverter c, params object?[] values) =>
        c.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

    [TestMethod]
    public void OverrideBeatsAppDefault()
    {
        var themeA = new BaseSlideTheme();
        var themeB = new BaseSlideTheme();
        foreach (var c in All())
        {
            Assert.AreEqual(false, Run(c, themeA, themeA.Id, themeB.Id), c.GetType().Name);
            Assert.AreEqual(true, Run(c, themeB, themeA.Id, themeB.Id), c.GetType().Name);
        }
    }

    [TestMethod]
    public void AppDefaultUsedWhenOverrideNullOrAbsent()
    {
        var theme = new BaseSlideTheme();
        foreach (var c in All())
        {
            Assert.AreEqual(true, Run(c, theme, theme.Id, null), c.GetType().Name);
            Assert.AreEqual(true, Run(c, theme, theme.Id), c.GetType().Name);
        }
    }

    [TestMethod]
    public void NoMatchOrNoIds_ReturnsFalse()
    {
        var theme = new BaseSlideTheme();
        var other = Guid.NewGuid();
        foreach (var c in All())
        {
            Assert.AreEqual(false, Run(c, theme, other, null), c.GetType().Name);
            Assert.AreEqual(false, Run(c, theme, null, null), c.GetType().Name);
            Assert.AreEqual(false, Run(c, theme), c.GetType().Name);
        }
    }
}
