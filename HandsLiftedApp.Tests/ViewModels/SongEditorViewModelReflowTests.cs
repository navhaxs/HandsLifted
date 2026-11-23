using System;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.ViewModels;

[TestClass]
public class SongEditorViewModelReflowTests
{
    private const string Six = "a\nb\nc\nd\ne\nf";

    [TestInitialize]
    public void Setup() => Globals.Instance.AppPreferences = new AppPreferencesViewModel();

    private static Task WithEditor(Action<SongEditorViewModel, SongItemInstance> body) =>
        DispatcherTestThread.Run(() =>
        {
            var data = new SongItem { Title = "T" };
            data.Stanzas.Add(new SongStanza(Guid.NewGuid(), "Verse 1", Six));
            data.Stanzas.Add(new SongStanza(Guid.NewGuid(), "Interlude", ""));
            data.Stanzas.Add(new SongStanza(Guid.NewGuid(), "Chorus", "a\nb\n\nc\nd\ne\nf"));
            foreach (var s in data.Stanzas) data.Arrangement.Add(s.Id);
            Globals.Instance.SongLibraryIndex.Register(data, "irrelevant.xml", "irrelevant");

            var playlist = new PlaylistInstance();
            var song = new SongItemInstance(playlist) { SongId = data.UUID };
            var vm = new SongEditorViewModel(song, playlist);
            vm.UndoStack.Clear();
            body(vm, song);
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task ReflowLyrics_Smart_RewritesAllStanzas() => WithEditor((vm, song) =>
    {
        vm.ReflowLyrics(ReflowMode.Smart);
        Assert.AreEqual("a\nb\nc\n\nd\ne\nf", song.Stanzas[0].Lyrics);
        Assert.AreEqual("a\nb\nc\n\nd\ne\nf", song.Stanzas[2].Lyrics);
    });

    [TestMethod]
    public Task ReflowLyrics_Fixed2_OverwritesExisting() => WithEditor((vm, song) =>
    {
        vm.ReflowLyrics(ReflowMode.Fixed(2));
        Assert.AreEqual("a\nb\n\nc\nd\n\ne\nf", song.Stanzas[2].Lyrics);
    });

    [TestMethod]
    public Task ReflowLyrics_EmptyStanzaUntouched() => WithEditor((vm, song) =>
    {
        vm.ReflowLyrics(ReflowMode.Smart);
        Assert.AreEqual("", song.Stanzas[1].Lyrics);
    });

    [TestMethod]
    public Task ReflowLyrics_IsOneUndoStep_UndoRestoresAll() => WithEditor((vm, song) =>
    {
        vm.ReflowLyrics(ReflowMode.Smart);
        Assert.IsTrue(vm.UndoStack.CanUndo);

        vm.UndoStack.Undo();

        Assert.AreEqual(Six, song.Stanzas[0].Lyrics);
        Assert.AreEqual("a\nb\n\nc\nd\ne\nf", song.Stanzas[2].Lyrics);
        Assert.IsFalse(vm.UndoStack.CanUndo, "reflow should be exactly one undo step");
    });

    [TestMethod]
    public Task ReflowLyrics_NoChange_RecordsNoUndoStep() => WithEditor((vm, song) =>
    {
        vm.ReflowLyrics(ReflowMode.Smart);
        vm.UndoStack.Clear();

        vm.ReflowLyrics(ReflowMode.Smart); // already reflowed

        Assert.IsFalse(vm.UndoStack.CanUndo);
    });
}
