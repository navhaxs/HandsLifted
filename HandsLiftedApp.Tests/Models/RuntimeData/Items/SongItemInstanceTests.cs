using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items
{
    [TestClass]
    public class SongItemInstanceTests
    {
        [TestMethod]
        public void UUID_NotYetRegistered_ResolvesAsMissing()
        {
            var instance = new SongItemInstance(null) { UUID = Guid.NewGuid() };

            Assert.IsTrue(instance.IsMissing);
            Assert.AreEqual("(Missing Song)", instance.Title);
        }

        [TestMethod]
        public void UUID_Registered_ForwardsPropertiesFromSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Amazing Grace", Copyright = "PD" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");

            var instance = new SongItemInstance(null) { UUID = song.UUID };

            Assert.IsFalse(instance.IsMissing);
            Assert.AreEqual("Amazing Grace", instance.Title);
            Assert.AreEqual("PD", instance.Copyright);
        }

        [TestMethod]
        public void SettingTitle_WritesThroughToSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var instance = new SongItemInstance(null) { UUID = song.UUID };

            instance.Title = "Edited";

            Assert.AreEqual("Edited", song.Title);
        }

        [TestMethod]
        public void EditingFromOneInstance_IsVisibleFromAnotherInstance_SameUUID()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var a = new SongItemInstance(null) { UUID = song.UUID };
            var b = new SongItemInstance(null) { UUID = song.UUID };

            a.Title = "Edited By A";

            Assert.AreEqual("Edited By A", b.Title);
        }

        [TestMethod]
        public void NewDraft_IsNotMissing_AndForwardsToLocalDraft()
        {
            var draft = SongItemInstance.NewDraft(null);
            draft.Title = "Draft Title";

            Assert.IsFalse(draft.IsMissing);
            Assert.AreEqual("Draft Title", draft.Title);
            Assert.IsNull(Globals.Instance.SongLibraryIndex.Resolve(draft.UUID),
                "A draft must not be registered in the shared index until AttachToLibrary is called");
        }

        // The SongChanged subscription in SongItemInstance's constructor uses
        // .ObserveOn(RxSchedulers.MainThreadScheduler), which posts through Avalonia's
        // Dispatcher.UIThread rather than delivering synchronously. Asserting on it therefore
        // needs the same DispatcherTestThread + RunJobs() pattern ScriptureItemInstanceTests
        // already established for this exact class of timing issue (see DispatcherTestThread's
        // own comments) - without it, the assertion below could pass even if disposal were
        // broken, simply because the queued notification hadn't been drained yet.
        [TestMethod]
        public Task Disposing_UnsubscribesFromSharedSongChanged() => DispatcherTestThread.Run(async () =>
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");

            var a = new SongItemInstance(null) { UUID = song.UUID };
            var b = new SongItemInstance(null) { UUID = song.UUID };

            var raisedAfterDispose = false;
            a.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SongItemInstance.Title))
                    raisedAfterDispose = true;
            };

            a.Dispose();

            // Write through a second, still-live instance referencing the same UUID.
            // Before the fix, `a`'s SongChanged subscription would still be alive and would
            // re-raise its own Title PropertyChanged in response once the dispatcher drains.
            b.Title = "Edited By B";
            for (int i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                if (raisedAfterDispose) break;
                await Task.Delay(50);
            }

            Assert.IsFalse(raisedAfterDispose,
                "A disposed SongItemInstance must not react to SongChanged notifications for its UUID");
        });
    }
}
