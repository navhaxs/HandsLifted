using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;
using HandsLiftedApp.Tests.Importer.Scripture;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureItemInstanceTests
{
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

    private static ScriptureSourceLoader MakeFakeLoader(string xml)
    {
        // Reuses Phase 1's internal FakeHttpMessageHandler (HandsLiftedApp.Tests/Importer/Scripture/FakeHttpMessageHandler.cs) —
        // `internal` is assembly-scoped, so it's visible here without changing its accessibility.
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) });
        return new ScriptureSourceLoader(new HttpClient(handler), System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + System.Guid.NewGuid().ToString("N")));
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_ProducesOneSlidePerVerseInRange()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
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

        Assert.AreEqual(2, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        var second = (ScriptureSlideInstance)instance.Slides[1];
        Assert.AreEqual("In the beginning God created the heaven and the earth.", first.Text);
        Assert.AreEqual("And the earth was without form, and void.", second.Text);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SlideLabel_UsesParsedBookTitle_NotBookCode()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 1
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.AreEqual("Genesis 1:1", first.Label);
    }

    [TestMethod]
    public async Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
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
    public async Task GenerateSlidesAsync_NarrowerRangeOnRegenerate_ShrinksSlideList()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 3
        };
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(3, instance.Slides.Count);

        instance.EndVerse = 2;
        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(2, instance.Slides.Count);
    }

    [TestMethod]
    public async Task ActiveSlide_TracksSelectedSlideIndex()
    {
        var instance = new ScriptureItemInstance(null, MakeFakeLoader(GenesisChapterOneUsx))
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

        instance.SelectedSlideIndex = 1;

        Assert.AreSame(instance.Slides[1], instance.ActiveSlide);
    }
}
