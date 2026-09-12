using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Importer.Scripture;
using HandsLiftedApp.Tests.TestSupport;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class PlaylistInstanceScriptureNavigationTests
{
    // DispatcherTestThread.EnsureStarted() is called from ScriptureItemInstanceTests's own
    // [AssemblyInitialize] -- MSTest allows only one per assembly, and it's guaranteed to run
    // before any test class in this assembly, so this class doesn't declare its own.

    [TestInitialize]
    public void Setup()
    {
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
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedPlaylistScriptureNavTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "gen.usx"), xml);
        return new ScriptureLocalUsxStore(tempDir);
    }

    // Regression test: navigating to a second scripture item whose active page happens to share
    // the same page index (and therefore the same Id, e.g. "page0") as the first item must not
    // leave the live output stuck on the first item's slide.
    [TestMethod]
    public Task NavigateToReference_ToSameSlideIndexInDifferentScriptureItem_ActiveSlideFollowsToSecondItem() => DispatcherTestThread.Run(async () =>
    {
        var playlist = new PlaylistInstance();

        var item1 = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            UUID = Guid.NewGuid(),
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 1,
        };
        var item2 = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
        {
            UUID = Guid.NewGuid(),
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 2,
            EndChapter = 1,
            EndVerse = 3,
        };

        playlist.Items.Add(item1);
        playlist.Items.Add(item2);

        await item1.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        await item2.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual("page0", ((HandsLiftedApp.Data.Slides.ScriptureSlideInstance)item1.Slides[0]).Id);
        Assert.AreEqual("page0", ((HandsLiftedApp.Data.Slides.ScriptureSlideInstance)item2.Slides[0]).Id);

        playlist.NavigateToReference(new SlideReference { ItemUUID = item1.UUID, SlideIndex = 0 });
        Assert.AreSame(item1.Slides[0], playlist.ActiveSlide);

        playlist.NavigateToReference(new SlideReference { ItemUUID = item2.UUID, SlideIndex = 0 });
        Assert.AreSame(item2.Slides[0], playlist.ActiveSlide);
    });
}
