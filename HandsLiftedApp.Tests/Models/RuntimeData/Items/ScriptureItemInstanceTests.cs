using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureItemInstanceTests
{
    [TestInitialize]
    public void Setup()
    {
        // ResolvedDesignTheme (and GenerateSlidesAsync's default-store fallback) read
        // Globals.Instance.AppPreferences, which is null unless Globals.OnStartup() has run.
        // Matches the same convention already used by SongImporterTests.Init().
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    private const string GenesisChapterOneUsx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <usx version="3.0">
          <book code="GEN" style="id">- Genesis</book>
          <para style="mt1">Genesis</para>
          <chapter number="1" style="c" sid="GEN 1"/>
          <para style="p">
            <verse number="1" style="v" sid="GEN 1:1"/>In the beginning God created the heaven and the earth.<verse eid="GEN 1:1"/>
            <verse number="2" style="v" sid="GEN 1:2"/>And the earth was without form, and void.<verse eid="GEN 1:2"/>
            <verse number="3" style="v" sid="GEN 1:3"/>And God said, Let there be light.<verse eid="GEN 1:3"/>
          </para>
          <chapter eid="GEN 1"/>
        </usx>
        """;

    private static ScriptureLocalUsxStore MakeFakeStore(string xml)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "gen.usx"), xml);
        return new ScriptureLocalUsxStore(tempDir);
    }

    private static ScriptureLocalUsxStore MakeEmptyStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        return new ScriptureLocalUsxStore(tempDir);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_ShortRange_ProducesOnePageWithHeaderAndVerseText()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.IsTrue(first.Lines.Any(l => l.IsHeader), "first slide must carry a header line");
        Assert.IsTrue(first.Text.Contains("In the beginning"), "flattened text should include verse content");
        Assert.IsTrue(first.Text.Contains("without form"), "flattened text should include the second verse too");
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SlideId_UsesPageIndexNotChapterVerse()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.AreEqual("page0", first.Id);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var firstCallSlide = instance.Slides[0];

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        var secondCallSlide = instance.Slides[0];

        Assert.AreSame(firstCallSlide, secondCallSlide);
    }

    [TestMethod]
    public async Task ActiveSlide_TracksSelectedSlideIndex()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        instance.SelectedSlideIndex = 0;

        Assert.AreSame(instance.Slides[0], instance.ActiveSlide);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_BookFileMissing_ProducesPlaceholderPage()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore())
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        var slide = (ScriptureSlideInstance)instance.Slides[0];
        StringAssert.Contains(slide.Text, "Scripture data not found");
        StringAssert.Contains(slide.Text, "gen");
    }

    [TestMethod]
    public void ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore());

        // With no ParentPlaylist and Design left at its Guid.Empty default, resolution
        // falls back to the app's default theme rather than throwing or returning null.
        Assert.IsNotNull(instance.ResolvedDesignTheme);
    }
}
