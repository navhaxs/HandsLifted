using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class LibraryViewModelScriptureExclusionTests
{
    [TestMethod]
    public void BuildLibraryOrNull_ScriptureType_ReturnsNull()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "KJV", Directory = "", Type = LibraryType.Scripture };

        Assert.IsNull(LibraryViewModel.BuildLibraryOrNull(def));
    }

    [TestMethod]
    public void BuildLibraryOrNull_MediaType_ReturnsLibrary()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "Media", Directory = "", Type = LibraryType.Media };

        Assert.IsNotNull(LibraryViewModel.BuildLibraryOrNull(def));
    }

    [TestMethod]
    public void BuildLibraryOrNull_SongType_ReturnsSongLibrary()
    {
        var def = new LibraryConfig.LibraryDefinition { Label = "Songs", Directory = "", Type = LibraryType.Song };

        Assert.IsInstanceOfType(LibraryViewModel.BuildLibraryOrNull(def), typeof(SongLibrary));
    }
}
