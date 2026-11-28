using System;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class SplitMediaGroupActionTests
{
    [TestInitialize]
    public void Setup() => Globals.Instance.AppPreferences = new AppPreferencesViewModel();

    private static string Paths(MediaGroupItem g) =>
        string.Join(",", g.Items.Select(i => ((MediaGroupItem.MediaItem)i).SourceMediaFilePath));

    private static void Fill(MediaGroupItem g, params string[] paths)
    {
        foreach (var path in paths) g.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = path });
    }

    [TestMethod]
    public void Split_GenericGroup_MovesTailToNewGroupAfterIt_AndUndoRedoRoundTrips()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "G" };
        Fill(g, "A", "B", "C", "D");
        p.Items.Add(g);
        p.UndoStack.Clear();

        p.UndoStack.Execute(new SplitMediaGroupAction(p, g, 2));

        Assert.AreEqual(2, p.Items.Count);
        Assert.AreEqual("A,B", Paths(g));
        var second = (MediaGroupItem)p.Items[1];
        Assert.IsInstanceOfType<MediaGroupItemInstance>(second);
        Assert.AreEqual("C,D", Paths(second));

        p.UndoStack.Undo();
        Assert.AreEqual(1, p.Items.Count);
        Assert.AreEqual("A,B,C,D", Paths(g));

        p.UndoStack.Redo();
        Assert.AreEqual(2, p.Items.Count);
        Assert.AreEqual("A,B", Paths(g));
        Assert.AreEqual("C,D", Paths((MediaGroupItem)p.Items[1]));
    }

    [TestMethod]
    public void Split_PdfGroup_NewGroupIsGenericAndKeepsNoPdfSource()
    {
        var p = new PlaylistInstance();
        var pdf = new PDFSlidesGroupItemInstance(p)
        {
            Title = "Deck",
            SourcePresentationFile = @"C:\x\deck.pdf",
        };
        Fill(pdf, "1.png", "2.png", "3.png");
        p.Items.Add(pdf);
        p.UndoStack.Clear();

        p.UndoStack.Execute(new SplitMediaGroupAction(p, pdf, 1));

        Assert.AreEqual("1.png", Paths(pdf));
        var second = (MediaGroupItem)p.Items[1];
        Assert.AreEqual(typeof(MediaGroupItemInstance), second.GetType());
        Assert.AreEqual("2.png,3.png", Paths(second));
        // the original is still the PDF
        Assert.IsInstanceOfType<PDFSlidesGroupItem>(p.Items[0]);
    }

    [TestMethod]
    public void Split_RepeatedlyFromTheTail_NumbersPartsInsteadOfStackingSuffixes()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "Deck" };
        Fill(g, "1", "2", "3", "4");
        p.Items.Add(g);
        p.UndoStack.Clear();

        p.UndoStack.Execute(new SplitMediaGroupAction(p, g, 1));
        p.UndoStack.Execute(new SplitMediaGroupAction(p, (MediaGroupItem)p.Items[1], 1));
        p.UndoStack.Execute(new SplitMediaGroupAction(p, (MediaGroupItem)p.Items[2], 1));

        CollectionAssert.AreEqual(
            new[] { "Deck", "Deck (part 2)", "Deck (part 3)", "Deck (part 4)" },
            p.Items.Select(i => i.Title).ToArray());
    }

    [TestMethod]
    public void Split_MiddlePart_TakesNextFreeNumber()
    {
        var p = new PlaylistInstance();
        foreach (var t in new[] { "Deck", "Deck (part 2)", "Deck (part 3)", "Other (part 9)" })
            p.Items.Add(new MediaGroupItemInstance(p) { Title = t });

        Assert.AreEqual("Deck (part 4)", SplitMediaGroupAction.NextPartTitle(p, "Deck (part 2)"));
        Assert.AreEqual("Deck (part 4)", SplitMediaGroupAction.NextPartTitle(p, "Deck"));
        Assert.AreEqual("Fresh (part 2)", SplitMediaGroupAction.NextPartTitle(p, "Fresh"));
        // a suffixed title with no unsuffixed sibling still continues from its own number
        Assert.AreEqual("Other (part 10)", SplitMediaGroupAction.NextPartTitle(p, "Other (part 9)"));
    }

    [TestMethod]
    public void Split_UndoRedo_KeepsTheSameNewTitle()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p) { Title = "G" };
        Fill(g, "A", "B");
        p.Items.Add(g);
        p.UndoStack.Clear();

        p.UndoStack.Execute(new SplitMediaGroupAction(p, g, 1));
        p.UndoStack.Undo();
        p.UndoStack.Redo();

        Assert.AreEqual("G (part 2)", p.Items[1].Title);
    }

    [TestMethod]
    public void CanSplit_RejectsFirstSlideAndOutOfRange()
    {
        var p = new PlaylistInstance();
        var g = new MediaGroupItemInstance(p);
        Fill(g, "A", "B");
        Assert.IsFalse(SplitMediaGroupAction.CanSplit(g, 0));
        Assert.IsTrue(SplitMediaGroupAction.CanSplit(g, 1));
        Assert.IsFalse(SplitMediaGroupAction.CanSplit(g, 2));
    }
}
