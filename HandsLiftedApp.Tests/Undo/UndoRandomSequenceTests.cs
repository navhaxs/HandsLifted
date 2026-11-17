using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

/// <summary>
/// Hardening: long random sequences of playlist edits (add / delete / move items, add / delete / move / re-point
/// slides in groups) must undo all the way back to the empty playlist, passing through exactly the states they
/// went through, and redo all the way forward again.
/// </summary>
[TestClass]
public class UndoRandomSequenceTests
{
    [TestInitialize]
    public void Setup() => Globals.Instance.AppPreferences = new AppPreferencesViewModel();

    private static string Signature(PlaylistInstance p) => string.Join(";", p.Items.Select(i =>
        i is MediaGroupItem g
            ? $"{i.UUID:N}|{i.Title}|" + string.Join(",", g.Items.Select(s => (s as MediaGroupItem.MediaItem)?.SourceMediaFilePath))
            : $"{i.UUID:N}|{i.Title}"));

    [TestMethod]
    public Task RandomEditSequences_UndoAndRedoThroughTheSameStates() => DispatcherTestThread.Run(() =>
    {
        for (var seed = 1; seed <= 25; seed++)
        {
            var rng = new Random(seed);
            var p = new PlaylistInstance();
            p.UndoStack.MaxDepth = 0; // unlimited: we want to walk the whole history
            var counter = 0;

            MediaGroupItemInstance? PickGroup() =>
                p.Items.OfType<MediaGroupItemInstance>().OrderBy(_ => rng.Next()).FirstOrDefault();

            var states = new List<string> { Signature(p) };

            for (var step = 0; step < 80; step++)
            {
                var before = states[^1];
                var changed = true;
                switch (rng.Next(7))
                {
                    case 0: // add a group, with a few slides
                    {
                        var g = new MediaGroupItemInstance(p) { Title = "G" + counter++ };
                        for (var i = rng.Next(0, 4); i > 0; i--)
                            g.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = "m" + counter++ });
                        p.UndoStack.Execute(new InsertPlaylistItemAction(p, g, rng.Next(0, p.Items.Count + 1)));
                        break;
                    }
                    case 1: // delete an item
                        if (p.Items.Count == 0) { changed = false; break; }
                        p.UndoStack.Execute(new RemovePlaylistItemAction(p, p.Items[rng.Next(p.Items.Count)]));
                        break;
                    case 2: // move an item
                    {
                        if (p.Items.Count < 2) { changed = false; break; }
                        var from = rng.Next(p.Items.Count);
                        var to = rng.Next(p.Items.Count);
                        if (from == to) { changed = false; break; }
                        p.UndoStack.Execute(new MovePlaylistItemAction(p, p.Items[from], from, to));
                        break;
                    }
                    case 3: // add a slide to a group
                    {
                        var g = PickGroup();
                        if (g == null) { changed = false; break; }
                        GroupSlideEdits.Insert(g, rng.Next(0, g.Items.Count + 1),
                            new MediaGroupItem.MediaItem { SourceMediaFilePath = "m" + counter++ });
                        break;
                    }
                    case 4: // delete a slide
                    {
                        var g = PickGroup();
                        if (g == null || g.Items.Count == 0) { changed = false; break; }
                        GroupSlideEdits.RemoveAt(g, rng.Next(g.Items.Count));
                        break;
                    }
                    case 5: // move a slide
                    {
                        var g = PickGroup();
                        if (g == null || g.Items.Count < 2) { changed = false; break; }
                        var from = rng.Next(g.Items.Count);
                        var to = rng.Next(g.Items.Count);
                        if (from == to) { changed = false; break; }
                        GroupSlideEdits.Move(g, from, to);
                        break;
                    }
                    default: // re-point a slide at another file
                    {
                        var g = PickGroup();
                        if (g == null || g.Items.Count == 0) { changed = false; break; }
                        GroupSlideEdits.SetMediaPath(g, rng.Next(g.Items.Count), "m" + counter++);
                        break;
                    }
                }

                var after = Signature(p);
                if (changed && after != before) states.Add(after);
            }

            Assert.IsTrue(states.Count > 25, $"seed {seed}: the sequence really changed the playlist ({states.Count} states)");

            // all the way back, state by state
            for (var i = states.Count - 1; i > 0; i--)
            {
                Assert.AreEqual(states[i], Signature(p), $"seed {seed}: state before undoing step {i}");
                Assert.IsTrue(p.UndoStack.Undo(), $"seed {seed}: undo step {i}");
            }

            Assert.AreEqual(states[0], Signature(p), $"seed {seed}: back at the empty playlist");
            Assert.IsFalse(p.UndoStack.CanUndo, $"seed {seed}: nothing left to undo");

            // and all the way forward again
            for (var i = 1; i < states.Count; i++)
            {
                Assert.IsTrue(p.UndoStack.Redo(), $"seed {seed}: redo step {i}");
                Assert.AreEqual(states[i], Signature(p), $"seed {seed}: after redoing step {i}");
            }

            Assert.IsFalse(p.UndoStack.CanRedo, $"seed {seed}: nothing left to redo");
        }

        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DepthLimit_KeepsOnlyTheNewestSteps_AndTheOldestSurvivorsStillUndoCleanly() => DispatcherTestThread.Run(() =>
    {
        var p = new PlaylistInstance();
        p.UndoStack.MaxDepth = 10;
        for (var i = 0; i < 50; i++)
            p.UndoStack.Execute(new InsertPlaylistItemAction(p, new LogoItemInstance(p) { Title = "L" + i }, p.Items.Count));

        var undone = 0;
        while (p.UndoStack.Undo()) undone++;

        Assert.AreEqual(10, undone, "older steps were forgotten");
        Assert.AreEqual(40, p.Items.Count, "the 40 forgotten adds stay in the playlist");
        return Task.CompletedTask;
    });
}
