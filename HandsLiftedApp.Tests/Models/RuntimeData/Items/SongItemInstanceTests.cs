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
        public void SongId_NotYetRegistered_ResolvesAsMissing()
        {
            var instance = new SongItemInstance(null) { SongId = Guid.NewGuid() };

            Assert.IsTrue(instance.IsMissing);
            Assert.AreEqual("(Missing Song)", instance.Title);
        }

        [TestMethod]
        public void SongId_Registered_ForwardsPropertiesFromSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Amazing Grace", Copyright = "PD" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");

            var instance = new SongItemInstance(null) { SongId = song.UUID };

            Assert.IsFalse(instance.IsMissing);
            Assert.AreEqual("Amazing Grace", instance.Title);
            Assert.AreEqual("PD", instance.Copyright);
        }

        [TestMethod]
        public void SettingTitle_WritesThroughToSharedSong()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var instance = new SongItemInstance(null) { SongId = song.UUID };

            instance.Title = "Edited";

            Assert.AreEqual("Edited", song.Title);
        }

        [TestMethod]
        public void EditingFromOneInstance_IsVisibleFromAnotherInstance_SameSongId()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Original" };
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
            var a = new SongItemInstance(null) { SongId = song.UUID };
            var b = new SongItemInstance(null) { SongId = song.UUID };

            // Two playlist items referencing the same song share a SongId but must keep
            // distinct playlist-item identities (UUID) — slide navigation and drag-reorder
            // both key off UUID and would resolve to the wrong item if they collided.
            Assert.AreNotEqual(a.UUID, b.UUID,
                "Two SongItemInstances referencing the same song must each have their own UUID");

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
            Assert.IsNull(Globals.Instance.SongLibraryIndex.Resolve(draft.SongId),
                "A draft must not be registered in the shared index until AttachToLibrary is called");
            Assert.AreNotEqual(Guid.Empty, draft.SongId, "NewDraft must assign a SongId");
            Assert.AreNotEqual(draft.UUID, draft.SongId,
                "A draft's playlist-item identity (UUID) must stay independent of the song it points at (SongId)");
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

            var a = new SongItemInstance(null) { SongId = song.UUID };
            var b = new SongItemInstance(null) { SongId = song.UUID };

            var raisedAfterDispose = false;
            a.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SongItemInstance.Title))
                    raisedAfterDispose = true;
            };

            a.Dispose();

            // Write through a second, still-live instance referencing the same SongId.
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
                "A disposed SongItemInstance must not react to SongChanged notifications for its SongId");
        });

        // Dispose() used to only dispose _songChangedSubscription. The constructor also attaches
        // CollectionChanged / CollectionItemChanged handlers to whatever Arrangement/Stanzas
        // resolve to — under the library facade, the *shared* library song's collections, held
        // for the app's lifetime by SongLibraryIndex. Without detaching them, a disposed instance
        // stayed rooted and kept running GenerateArrangementViews() (+ a debounced
        // UpdateStanzaSlides()) on every future edit of that song.
        [TestMethod]
        public void Disposing_DetachesFromSharedSongCollections()
        {
            var song = new HandsLiftedApp.Data.Models.Items.SongItem { Title = "Shared" };
            var firstStanza = new HandsLiftedApp.Data.Models.Items.SongStanza { Name = "Verse 1", Lyrics = "one" };
            song.Stanzas.Add(firstStanza);
            song.Arrangement.Add(firstStanza.Id);
            Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");

            var instance = new SongItemInstance(null) { SongId = song.UUID };
            // ItemInstanceFactory does exactly this after the object initializer — it re-fires the
            // constructor's WhenAnyValue(Arrangement)/WhenAnyValue(Stanzas) subscriptions so their
            // handlers attach to the real shared collections rather than the throwaway empty ones
            // the facade getters returned while SongId was still Guid.Empty during construction.
            instance.RaiseForwardedPropertiesChanged();

            Assert.AreEqual(1, instance.ArrangementAsRefList.Count,
                "Precondition: the instance must be wired to the shared song's collections");

            instance.Dispose();

            var secondStanza = new HandsLiftedApp.Data.Models.Items.SongStanza { Name = "Verse 2", Lyrics = "two" };
            song.Stanzas.Add(secondStanza);
            song.Arrangement.Add(secondStanza.Id);

            Assert.AreEqual(1, instance.ArrangementAsRefList.Count,
                "A disposed SongItemInstance must not keep regenerating its arrangement views in " +
                "response to edits of the shared library song's collections");
        }

        [TestMethod]
        public void MissingSong_GeneratesSinglePlaceholderSlide()
        {
            var instance = new SongItemInstance(null) { SongId = Guid.NewGuid() };

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
