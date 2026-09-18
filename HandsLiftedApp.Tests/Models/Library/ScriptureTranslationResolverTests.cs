using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class ScriptureTranslationResolverTests
{
    private static LibraryConfig.LibraryDefinition MakeTranslation(string label, string directory) =>
        new() { Label = label, Directory = directory, Type = LibraryType.Scripture };

    [TestMethod]
    public void ResolveDirectory_MatchingLabel_ReturnsItsDirectory()
    {
        var configured = new List<LibraryConfig.LibraryDefinition>
        {
            MakeTranslation("BSB", @"C:\Scripture\BSB"),
            MakeTranslation("KJV", @"C:\Scripture\KJV")
        };

        var result = ScriptureTranslationResolver.ResolveDirectory("KJV", configured);

        Assert.AreEqual(@"C:\Scripture\KJV", result);
    }

    [TestMethod]
    public void ResolveDirectory_NoMatchingLabel_FallsBackToFirstConfigured()
    {
        var configured = new List<LibraryConfig.LibraryDefinition>
        {
            MakeTranslation("BSB", @"C:\Scripture\BSB"),
            MakeTranslation("KJV", @"C:\Scripture\KJV")
        };

        var result = ScriptureTranslationResolver.ResolveDirectory("NIV", configured);

        Assert.AreEqual(@"C:\Scripture\BSB", result);
    }

    [TestMethod]
    public void ResolveDirectory_NoneConfigured_ReturnsEmptyString()
    {
        var result = ScriptureTranslationResolver.ResolveDirectory("BSB", new List<LibraryConfig.LibraryDefinition>());

        Assert.AreEqual("", result);
    }
}
