using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.Thumbnail;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Importer.Scripture;
using SkiaSharp;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class ScriptureItemInstanceTests
{
    // BitmapUtils.SKBitmapToAvalonia (used by GenerateSlidesAsync_ForceInvalidateCache_ResetsCachedOnReusedSlide
    // below) constructs an Avalonia.Media.Imaging.Bitmap, which requires a registered
    // IPlatformRenderInterface. Nothing else in this test host process sets that up. Registering just
    // the Skia render interface directly (rather than a full AppBuilder.UsePlatformDetect(), which also
    // brings up a Win32 windowing subsystem) is enough for Bitmap construction and avoids any dependency
    // on an interactive window station.
    //
    // This platform init must run on DispatcherTestThread's dedicated thread, not on whatever thread
    // MSTest happens to invoke [AssemblyInitialize] on - see the comment on DispatcherTestThread below for
    // why (Dispatcher.UIThread's owning thread is bound permanently on first access, and getting that
    // first access to land on a thread the tests control is the whole fix).
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        DispatcherTestThread.EnsureStarted();
    }

    [TestInitialize]
    public void Setup()
    {
        // ResolvedDesignTheme (and GenerateSlidesAsync's default-store fallback) read
        // Globals.Instance.AppPreferences, which is null unless Globals.OnStartup() has run.
        // Matches the same convention already used by SongImporterTests.Init().
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    // Fixes a thread-affinity flake: Dispatcher.UIThread binds permanently to whichever thread first
    // *accesses* it (lazy construction on first touch - Avalonia platform init alone does not trigger
    // this). ScriptureItemInstance.GenerateSlidesAsync awaits its disk read with ConfigureAwait(false) and
    // then calls Dispatcher.UIThread.Post(...) in the continuation, so - before this fix - that Post call,
    // running on whatever arbitrary ThreadPool thread happened to complete the disk read, was frequently
    // the FIRST thing in the whole test run to touch Dispatcher.UIThread, permanently binding it to a
    // ThreadPool thread instead of any thread the test controls. Later, when a test's own
    // `await instance.GenerateSlidesAsync(); Dispatcher.UIThread.RunJobs();` executed that queued job,
    // Avalonia's AvaloniaSynchronizationContext.Ensure (called from DispatcherOperation.Execute inside
    // RunJobs) calls Dispatcher.VerifyAccess() and throws "different thread owns it" unless RunJobs()
    // happens to be running on that same ThreadPool thread - a coin flip that depends on ThreadPool
    // scheduling, hence the non-determinism (worse under full-suite contention, but possible even in a
    // single sequential run).
    //
    // The fix has two parts, both required: (1) DispatcherTestThread.EnsureStarted() forces the first-ever
    // touch of Dispatcher.UIThread to happen on one dedicated background thread, before any test or
    // production code gets a chance to touch it first from elsewhere - so Dispatcher.UIThread is
    // permanently bound to a thread the tests actually control. (2) DispatcherTestThread.Run() then runs
    // every Dispatcher-touching test body on that same thread, via a SynchronizationContext installed on
    // it, so any `await` without ConfigureAwait(false) posts its continuation back onto that thread instead
    // of resuming on whatever thread completed the antecedent task - meaning `RunJobs()` always ends up
    // called from the one thread Dispatcher.UIThread is bound to. The dedicated thread runs a simple
    // perpetual pump loop, so posted continuations are always eventually run - unlike Dispatcher.UIThread's
    // own queue, which only drains when a test explicitly calls RunJobs().
    private static class DispatcherTestThread
    {
        private sealed class QueueSynchronizationContext(BlockingCollection<(SendOrPostCallback Callback, object? State)> queue) : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

            public override void Send(SendOrPostCallback d, object? state) => d(state);
        }

        private static readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> Queue = new();
        private static readonly object InitLock = new();
        private static bool _started;

        public static void EnsureStarted()
        {
            lock (InitLock)
            {
                if (_started) return;
                _started = true;

                using var ready = new ManualResetEventSlim();
                var thread = new Thread(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(new QueueSynchronizationContext(Queue));
                    Avalonia.Skia.SkiaPlatform.Initialize();
                    ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithPlatformServices().BuildApp();

                    // Dispatcher.UIThread is lazily constructed on whichever thread first accesses it -
                    // SkiaPlatform.Initialize() only registers render services, it does not itself touch
                    // the dispatcher. Force that first touch here, on this thread, before returning control
                    // to AssemblyInit - otherwise production code's own Dispatcher.UIThread.Post(...) call
                    // (reached via a ConfigureAwait(false) continuation, so on an arbitrary ThreadPool
                    // thread) would win the race and bind the dispatcher to the wrong thread instead.
                    Dispatcher.UIThread.VerifyAccess();
                    ready.Set();

                    foreach (var (callback, state) in Queue.GetConsumingEnumerable())
                    {
                        callback(state);
                    }
                })
                {
                    IsBackground = true,
                    Name = "ScriptureItemInstanceTests.DispatcherTestThread"
                };
                thread.Start();
                ready.Wait();
            }
        }

        public static Task Run(Func<Task> body)
        {
            EnsureStarted();
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Queue.Add((async _ =>
            {
                try
                {
                    await body();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null));
            return tcs.Task;
        }
    }

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

    private static ScriptureLocalUsxStore MakeFakeStore(string xml)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "gen.usx"), xml);
        return new ScriptureLocalUsxStore(tempDir);
    }

    private static ScriptureLocalUsxStore MakeEmptyStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HandsLiftedScriptureItemInstanceTests_" + Guid.NewGuid().ToString("N"));
        return new ScriptureLocalUsxStore(tempDir);
    }

    [TestMethod]
    public Task GenerateSlidesAsync_ShortRange_ProducesOnePageWithHeaderAndVerseText() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
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

        Assert.AreEqual(1, instance.Slides.Count);
        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.IsTrue(first.Lines.Any(l => l.IsHeader), "first slide must carry a header line");
        Assert.IsTrue(first.Text.Contains("In the beginning"), "flattened text should include verse content");
        Assert.IsTrue(first.Text.Contains("without form"), "flattened text should include the second verse too");
    });

    [TestMethod]
    public Task GenerateSlidesAsync_SlideId_UsesPageIndexNotChapterVerse() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
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

        var first = (ScriptureSlideInstance)instance.Slides[0];
        Assert.AreEqual("page0", first.Id);
    });

    [TestMethod]
    public Task GenerateSlidesAsync_SecondCallWithSameRange_PreservesSlideIdentity() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
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
    });

    [TestMethod]
    public Task ActiveSlide_TracksSelectedSlideIndex() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
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

        instance.SelectedSlideIndex = 0;

        Assert.AreSame(instance.Slides[0], instance.ActiveSlide);
    });

    [TestMethod]
    public Task GenerateSlidesAsync_BookFileMissing_ProducesPlaceholderPage() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore())
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

        Assert.AreEqual(1, instance.Slides.Count);
        var slide = (ScriptureSlideInstance)instance.Slides[0];
        StringAssert.Contains(slide.Text, "Scripture data not found");
        StringAssert.Contains(slide.Text, "gen");
    });

    [TestMethod]
    public void ResolvedDesignTheme_DesignEmpty_FallsBackToDefaultTheme()
    {
        var instance = new ScriptureItemInstance(null, MakeEmptyStore());

        // With no ParentPlaylist and Design left at its Guid.Empty default, resolution
        // falls back to the app's default theme rather than throwing or returning null.
        Assert.IsNotNull(instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_PlaylistScriptureDefaultSet_UsesPlaylistDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureTheme = new BaseSlideTheme { Name = "Scripture Theme" };
        playlist.Designs.Add(scriptureTheme);
        playlist.DefaultScriptureThemeId = scriptureTheme.Id;

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());

        Assert.AreSame(scriptureTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_ExplicitDesignOverridesPlaylistScriptureDefault()
    {
        var playlist = new PlaylistInstance();
        var scriptureDefaultTheme = new BaseSlideTheme { Name = "Scripture Default" };
        var explicitTheme = new BaseSlideTheme { Name = "Explicit" };
        playlist.Designs.Add(scriptureDefaultTheme);
        playlist.Designs.Add(explicitTheme);
        playlist.DefaultScriptureThemeId = scriptureDefaultTheme.Id;

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore())
        {
            Design = explicitTheme.Id
        };

        Assert.AreSame(explicitTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_PlaylistScriptureDefaultUnset_FallsBackToAppDefault()
    {
        var playlist = new PlaylistInstance();
        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());

        Assert.AreSame(Globals.Instance.AppPreferences.DefaultTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public void ResolvedDesignTheme_RaisesPropertyChanged_WhenPlaylistScriptureDefaultChanges()
    {
        var playlist = new PlaylistInstance();
        var scriptureTheme = new BaseSlideTheme { Name = "Scripture Theme" };
        playlist.Designs.Add(scriptureTheme);

        var instance = new ScriptureItemInstance(playlist, MakeEmptyStore());
        var raised = false;
        instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScriptureItemInstance.ResolvedDesignTheme)) raised = true;
        };

        playlist.DefaultScriptureThemeId = scriptureTheme.Id;

        Assert.IsTrue(raised);
        Assert.AreSame(scriptureTheme, instance.ResolvedDesignTheme);
    }

    [TestMethod]
    public Task PlaylistScriptureDefaultChanges_RepaginatesAndUpdatesGeneratedSlideTheme() => DispatcherTestThread.Run(async () =>
    {
        var playlist = new PlaylistInstance();
        var initialTheme = new BaseSlideTheme { Name = "Initial Scripture Theme" };
        var newTheme = new BaseSlideTheme { Name = "New Scripture Theme" };
        playlist.Designs.Add(initialTheme);
        playlist.Designs.Add(newTheme);
        playlist.DefaultScriptureThemeId = initialTheme.Id;

        var instance = new ScriptureItemInstance(playlist, MakeFakeStore(GenesisChapterOneUsx))
        {
            Translation = "eng_bsb",
            Book = "gen",
            StartChapter = 1,
            StartVerse = 1,
            EndChapter = 1,
            EndVerse = 2
            // Design intentionally left at Guid.Empty - this item rides the playlist default.
        };

        await instance.GenerateSlidesAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.AreEqual(1, instance.Slides.Count);
        Assert.AreSame(initialTheme, ((ScriptureSlideInstance)instance.Slides[0]).Theme,
            "sanity check: generated slide should start out on the initial playlist default theme");

        playlist.DefaultScriptureThemeId = newTheme.Id;

        // The repagination subscription debounces via a real DebounceDispatcher(200ms) timer
        // (not the Avalonia Dispatcher), so give it time to fire before pumping the UI-thread
        // jobs its continuation posts (RepaginateFromCache -> UpdatePages -> Dispatcher.UIThread.Post).
        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();

        Assert.AreSame(newTheme, instance.ResolvedDesignTheme);
        Assert.AreSame(newTheme, ((ScriptureSlideInstance)instance.Slides[0]).Theme,
            "changing the playlist scripture default must actually re-theme the generated slide, not just ResolvedDesignTheme");
    });

    [TestMethod]
    public Task GenerateSlidesAsync_ForceInvalidateCache_ResetsCachedOnReusedSlide() => DispatcherTestThread.Run(async () =>
    {
        var instance = new ScriptureItemInstance(null, MakeFakeStore(GenesisChapterOneUsx))
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
        var slide = (ScriptureSlideInstance)instance.Slides[0];

        using var skBitmap = new SKBitmap(1, 1);
        slide.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
        Assert.IsNotNull(slide.Cached);

        await instance.GenerateSlidesAsync(forceInvalidateCache: true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsNull(slide.Cached, "forceInvalidateCache must reset Cached even when content didn't change");
    });
}
