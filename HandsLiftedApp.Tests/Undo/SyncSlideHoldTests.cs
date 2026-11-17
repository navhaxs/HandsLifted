using System;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

/// <summary>After a Sync the live slide is held, so the audience isn't interrupted; the next navigation releases it.</summary>
[TestClass]
public class SyncSlideHoldTests
{
    [TestInitialize]
    public void Setup() => Globals.Instance.AppPreferences = new AppPreferencesViewModel();

    private static void Load(PDFSlidesGroupItemInstance pdf, params string[] paths)
    {
        pdf.Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(
            paths.Select(p => (MediaGroupItem.GroupItem)new MediaGroupItem.MediaItem { SourceMediaFilePath = p }).ToList());
        pdf.GenerateSlides();
    }

    private static async Task Settle() => await Task.Delay(150); // item's ActiveSlide hops to the main thread

    [TestMethod]
    public Task LiveSlideIsHeldAcrossASync_AndReleasedByNavigating() => DispatcherTestThread.Run(async () =>
    {
        var p = new PlaylistInstance();
        var pdf = new PDFSlidesGroupItemInstance(p);
        p.Items.Add(pdf);
        Load(pdf, "a.png", "b.png", "c.png");
        p.SelectedItemIndex = 0;
        pdf.SelectedSlideIndex = 1;
        await Settle();
        var live = pdf.ActiveSlide;
        Assert.AreSame(pdf._Slides[1], live);

        // what a Sync does
        SyncSlideHold.Begin(pdf, p);
        Load(pdf, "x.png", "y.png", "z.png");
        await Settle();
        Assert.AreSame(live, pdf.ActiveSlide, "the old live slide stays live");
        Assert.AreSame(live, p.ActiveSlide);

        // clicking the same index is a real selection now
        p.NavigateToReference(new SlideReference { ItemIndex = 0, SlideIndex = 1 });
        await Settle();
        Assert.IsNull(pdf.HeldSlide);
        Assert.AreSame(pdf._Slides[1], pdf.ActiveSlide);
    });

    [TestMethod]
    public Task NextAfterASync_MovesFromTheOldIndexIntoTheNewSlides() => DispatcherTestThread.Run(async () =>
    {
        var p = new PlaylistInstance();
        var pdf = new PDFSlidesGroupItemInstance(p);
        p.Items.Add(pdf);
        Load(pdf, "a.png", "b.png", "c.png");
        p.SelectedItemIndex = 0;
        pdf.SelectedSlideIndex = 0;
        await Settle();

        SyncSlideHold.Begin(pdf, p);
        Load(pdf, "x.png", "y.png", "z.png");
        await Settle();

        p.NavigateToReference(p.GetNextSlide());
        await Settle();
        Assert.AreEqual(1, pdf.SelectedSlideIndex);
        Assert.AreSame(pdf._Slides[1], pdf.ActiveSlide);
        Assert.IsNull(pdf.HeldSlide);
    });

    [TestMethod]
    public Task NothingIsHeldWhenTheItemIsNotLive() => DispatcherTestThread.Run(async () =>
    {
        var p = new PlaylistInstance();
        var pdf = new PDFSlidesGroupItemInstance(p);
        var other = new MediaGroupItemInstance(p);
        p.Items.Add(other);
        p.Items.Add(pdf);
        Load(pdf, "a.png", "b.png");
        p.SelectedItemIndex = 0; // another item is live
        await Settle();

        SyncSlideHold.Begin(pdf, p);
        Assert.IsNull(pdf.HeldSlide);
    });
}
