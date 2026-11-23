using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class LibrarySearchTests
    {
        private string _libraryDir = "";

        [TestInitialize]
        public void Setup()
        {
            _libraryDir = Path.Combine(Path.GetTempPath(), "LibrarySearchTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_libraryDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_libraryDir))
                    Directory.Delete(_libraryDir, recursive: true);
            }
            catch (IOException)
            {
                // a scan's debounced UUID-stamping save can still land here; don't fail the test over cleanup
            }
        }

        private void WriteSong(string title, string lyrics)
        {
            var song = new SongItem
            {
                Title = title,
                Stanzas = new TrulyObservableCollection<SongStanza>
                {
                    new SongStanza(Guid.NewGuid(), "Verse 1", lyrics)
                }
            };
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SongItem));
            using var stream = new FileStream(Path.Combine(_libraryDir, title + ".xml"), FileMode.Create);
            serializer.Serialize(stream, song);
        }

        // SongLibrary.BuildIndexAsync flips IsIndexReady via Dispatcher.UIThread.InvokeAsync, which only drains
        // when RunJobs() is called - so every wait loop here pumps it (see SongLibraryTests for the full story).
        private static async Task<bool> WaitFor(Func<bool> condition)
        {
            for (var i = 0; i < 100 && !condition(); i++)
            {
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
            }

            return condition();
        }

        [TestMethod]
        public Task Observe_SongLibrary_MatchesLyricsNotJustTitles_AndReSearchesWhenIndexIsReady() =>
            DispatcherTestThread.Run(async () =>
            {
                WriteSong("Amazing Grace", "Amazing grace how sweet the sound");
                WriteSong("Holy Holy Holy", "Holy holy holy Lord God Almighty");

                var config = new LibraryConfig.LibraryDefinition { Label = "Songs", Directory = _libraryDir };
                var library = new SongLibrary(config, new FileSystemSongLibrarySource(_libraryDir));

                var term = new BehaviorSubject<string?>("sweet");
                IReadOnlyList<string>? latest = null;
                using var subscription = LibrarySearch.Observe(term, new[] { library })
                    .Subscribe(items => latest = items.Select(i => i.Title).ToList());

                // "sweet" appears only in a lyric. Before the index is ready the search falls back to titles
                // (no match); once it is ready Observe must re-run the same term by itself.
                Assert.IsTrue(await WaitFor(() => latest != null && latest.SequenceEqual(new[] { "Amazing Grace" })),
                    $"lyric search did not find the song; last result: [{string.Join(", ", latest ?? Array.Empty<string>())}]");

                term.OnNext("zzzzqq");
                Assert.IsTrue(await WaitFor(() => latest != null && latest.Count == 0),
                    "a no-match term should return nothing");

                term.OnNext("");
                Assert.IsTrue(await WaitFor(() => latest != null && latest.Count == 2),
                    "an empty term should list every song");
            });
    }
}
