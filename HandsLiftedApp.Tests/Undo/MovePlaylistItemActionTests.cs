using System.Linq;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Undo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class MovePlaylistItemActionTests
{
    // Runtime instances, as in the real app: selecting an item makes PlaylistInstance ask it for its slides,
    // which a bare data item (e.g. SectionHeadingItem) can't answer.
    private static (PlaylistInstance playlist, LogoItemInstance a, LogoItemInstance b, LogoItemInstance c) Make()
    {
        var playlist = new PlaylistInstance();
        var a = new LogoItemInstance(playlist) { Title = "A" };
        var b = new LogoItemInstance(playlist) { Title = "B" };
        var c = new LogoItemInstance(playlist) { Title = "C" };
        playlist.Items.Add(a);
        playlist.Items.Add(b);
        playlist.Items.Add(c);
        return (playlist, a, b, c);
    }

    private static string Order(PlaylistInstance p) => string.Concat(p.Items.Select(i => i.Title));

    [TestMethod]
    public void Move_ThenUndo_ThenRedo_RestoresOrder()
    {
        var (p, a, _, _) = Make();

        p.UndoStack.Execute(new MovePlaylistItemAction(p, a, 0, 2, "Move Item"));
        Assert.AreEqual("BCA", Order(p));

        p.UndoStack.Undo();
        Assert.AreEqual("ABC", Order(p));

        p.UndoStack.Redo();
        Assert.AreEqual("BCA", Order(p));
    }

    [TestMethod]
    public void Move_PreservesSelection_OnDoAndUndo()
    {
        var (p, a, b, _) = Make();
        p.SelectedItemIndex = 1; // B selected

        p.UndoStack.Execute(new MovePlaylistItemAction(p, a, 0, 2, "Move Item"));
        Assert.AreSame(b, p.Items[p.SelectedItemIndex], "B still selected after A moved past it");

        p.UndoStack.Undo();
        Assert.AreSame(b, p.Items[p.SelectedItemIndex]);
    }

    [TestMethod]
    public void Undo_TracksItemByIdentity_EvenAfterOtherItemsMoved()
    {
        var (p, a, b, c) = Make();

        p.UndoStack.Execute(new MovePlaylistItemAction(p, a, 0, 1, "Move Item Down")); // BAC
        p.Items.Move(2, 0);                                                            // CBA (an unrelated edit)

        p.UndoStack.Undo(); // must put A back to its recorded index, wherever A is now

        Assert.AreEqual("A", p.Items[0].Title);
    }

    [TestMethod]
    public void Undo_WhenItemWasRemoved_FailsSafely_AndClearsHistory()
    {
        var (p, a, _, _) = Make();
        p.UndoStack.Execute(new MovePlaylistItemAction(p, a, 0, 1, "Move Item Down"));
        p.Items.Remove(a);

        Assert.IsFalse(p.UndoStack.Undo());

        Assert.IsFalse(p.UndoStack.CanUndo);
        Assert.AreEqual(2, p.Items.Count);
    }

    [TestMethod]
    public void EachPlaylistHasItsOwnHistory()
    {
        var (p1, a, _, _) = Make();
        var p2 = new PlaylistInstance();

        p1.UndoStack.Execute(new MovePlaylistItemAction(p1, a, 0, 1, "Move Item Down"));

        Assert.IsTrue(p1.UndoStack.CanUndo);
        Assert.IsFalse(p2.UndoStack.CanUndo);
    }
}
