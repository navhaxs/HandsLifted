using System;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
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
    }
}
