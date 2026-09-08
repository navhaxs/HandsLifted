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

        // Regression test for a bug in NewDraft's construction order: the SongItemInstance
        // constructor's WhenAnyValue(x => x.Stanzas)/WhenAnyValue(x => x.Arrangement) subscriptions
        // run *before* NewDraft assigns _localDraft, so at subscribe time ResolvedSong is still
        // null and the Stanzas/Arrangement facade getters each return a fresh, throwaway, empty
        // collection - the CollectionChanged handler (OnArrangementCollectionChanged) ends up wired
        // to that discarded collection, not the real draft's Stanzas/Arrangement. Once _localDraft
        // is assigned, draft.Stanzas/draft.Arrangement start returning the real, persistent
        // collections, but without re-running those subscriptions nothing ever rewires the handler
        // onto them - so mutating draft.Stanzas/draft.Arrangement in place (e.g. `.Add(...)`, as the
        // song editor UI does) silently fails to trigger GenerateArrangementViews()/UpdateStanzaSlides().
        // NewDraft's fix (calling RaiseForwardedPropertiesChanged() after _localDraft is set) forces
        // those WhenAnyValue subscriptions to re-fire against the real collections.
        [TestMethod]
        public void NewDraft_MutatingStanzasAndArrangementInPlace_TriggersArrangementViewRegeneration()
        {
            var draft = SongItemInstance.NewDraft(null);

            var stanza = new HandsLiftedApp.Data.Models.Items.SongStanza { Name = "Verse 1", Lyrics = "Line one" };
            draft.Stanzas.Add(stanza);
            draft.Arrangement.Add(stanza.Id);

            Assert.AreEqual(1, draft.ArrangementAsRefList.Count,
                "Adding a stanza to a NewDraft instance's Stanzas collection and referencing it from " +
                "Arrangement must trigger GenerateArrangementViews() via the collection's CollectionChanged " +
                "event - this only happens if the constructor's handler got rewired onto the real draft " +
                "collections instead of staying attached to the throwaway ones built before _localDraft " +
                "was assigned.");
            Assert.AreEqual(stanza.Id, draft.ArrangementAsRefList[0].SongStanza.Id);
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

        [TestMethod]
        public void MissingSong_GeneratesSinglePlaceholderSlide()
        {
            var instance = new SongItemInstance(null) { UUID = Guid.NewGuid() };

            instance.GenerateSlides();

            Assert.AreEqual(1, instance.Slides.Count);
            Assert.IsInstanceOfType(instance.Slides[0], typeof(HandsLiftedApp.Data.Slides.SongSlideInstance));
            // Distinguishes the intended "(Missing Song)" placeholder from the pre-existing
            // EndOnBlankSlide "BLANK" slide, which - in this synchronous test where the
            // Dispatcher is never pumped - is coincidentally also the only slide produced
            // today (TitleSlide's ToProperty chain never emits without a pumped Dispatcher,
            // so the title-slide branch is skipped even before this fix). Asserting on
            // content, not just count/type, ensures this test actually exercises the new
            // missing-song short-circuit rather than passing for an unrelated reason.
            var placeholder = (HandsLiftedApp.Data.Slides.SongSlideInstance)instance.Slides[0];
            Assert.AreEqual("MISSING", placeholder.Id);
            Assert.AreEqual("(Missing Song)", placeholder.Text);
        }
    }
}
