using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models.Items;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

/// <summary>
/// Guards against the class of bug where a persisted property can be edited without the playlist
/// noticing. For every property the playlist file stores, editing it must (a) raise the change event
/// path, so the dirty indicator reacts, and (b) actually alter the serialized document, so the edit is
/// really saved and DirtyTracker can see it. A failure of (b) means the property is not persisted at
/// all (a serializer gap); a failure of (a) means the event wiring is missing (the tracker's periodic
/// check would still catch it, but late).
///
/// Add a case here whenever a new persisted property is added.
/// </summary>
[TestClass]
public class PlaylistDirtyCoverageTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistDirtyCoverageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Globals.Instance.AppPreferences = new AppPreferencesViewModel();
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private sealed record Case(string Name, Func<PlaylistInstance, Action> Arrange);

    private static SongItem RegisterSong(string title)
    {
        var song = new SongItem { Title = title };
        Globals.Instance.SongLibraryIndex.Register(song, "irrelevant.xml", "irrelevant");
        return song;
    }

    // Arrange builds the item (adding it to the playlist) and returns the single edit to apply.
    private static IEnumerable<Case> AllCases()
    {
        // ---- playlist level ----
        yield return new("Playlist.Title", p => () => p.Title = "changed");
        yield return new("Playlist.LogoGraphicFile", p => () => p.LogoGraphicFile = Path.Combine(Path.GetTempPath(), "logo.png"));
        yield return new("Playlist.SlideTransitionDurationMs", p => () => p.SlideTransitionDurationMs += 50);
        yield return new("Playlist.DefaultSongThemeId", p => () => p.DefaultSongThemeId = Guid.NewGuid());
        yield return new("Playlist.DefaultSongMotionThemeId", p => () => p.DefaultSongMotionThemeId = Guid.NewGuid());
        yield return new("Playlist.DefaultScriptureThemeId", p => () => p.DefaultScriptureThemeId = Guid.NewGuid());

        // ---- section heading / comment (data classes added directly) ----
        yield return new("SectionHeading.Title", p =>
        {
            var i = new SectionHeadingItem { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("Comment.Title", p =>
        {
            var i = new CommentItem { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });

        // ---- logo ----
        yield return new("Logo.Title", p =>
        {
            var i = new LogoItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("Logo.SlideTransitionDurationMs", p =>
        {
            var i = new LogoItemInstance(p);
            p.Items.Add(i);
            return () => i.SlideTransitionDurationMs = 333;
        });

        // ---- song (reference into the library) ----
        yield return new("Song.SlideTransitionDurationMs", p =>
        {
            var i = new SongItemInstance(p) { SongId = RegisterSong("S1").UUID };
            p.Items.Add(i);
            return () => i.SlideTransitionDurationMs = 333;
        });
        yield return new("Song.MotionBackgroundVideoOverride", p =>
        {
            var i = new SongItemInstance(p) { SongId = RegisterSong("S1").UUID };
            p.Items.Add(i);
            return () => i.MotionBackgroundVideoOverride = @"C:\Videos\override.mp4";
        });
        yield return new("Song.SongId", p =>
        {
            var other = RegisterSong("S2");
            var i = new SongItemInstance(p) { SongId = RegisterSong("S1").UUID };
            p.Items.Add(i);
            return () => i.SongId = other.UUID;
        });

        // ---- scripture ----
        foreach (var (name, edit) in new (string, Action<ScriptureItemInstance>)[]
                 {
                     ("Title", s => s.Title = "John 3:16"),
                     ("Translation", s => s.Translation = "KJV"),
                     ("Book", s => s.Book = "JHN"),
                     ("StartChapter", s => s.StartChapter = 2),
                     ("StartVerse", s => s.StartVerse = 2),
                     ("EndChapter", s => s.EndChapter = 3),
                     ("EndVerse", s => s.EndVerse = 4),
                     ("Design", s => s.Design = Guid.NewGuid()),
                     ("SlideTransitionDurationMs", s => s.SlideTransitionDurationMs = 333),
                 })
        {
            yield return new($"Scripture.{name}", p =>
            {
                var i = new ScriptureItemInstance(p) { Title = "a", Book = "GEN", StartChapter = 1, StartVerse = 1, EndChapter = 1, EndVerse = 1 };
                p.Items.Add(i);
                return () => edit(i);
            });
        }

        // ---- media group ----
        yield return new("MediaGroup.Title", p =>
        {
            var i = new MediaGroupItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("MediaGroup.SlideTransitionDurationMs", p =>
        {
            var i = new MediaGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.SlideTransitionDurationMs = 333;
        });
        yield return new("MediaGroup.Items.Add", p =>
        {
            var i = new MediaGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });
        });
        yield return new("MediaGroup.Items.Remove", p =>
        {
            var i = new MediaGroupItemInstance(p);
            i.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });
            p.Items.Add(i);
            return () => i.Items.RemoveAt(0);
        });
        yield return new("MediaGroup.Items.Move", p =>
        {
            var i = new MediaGroupItemInstance(p);
            i.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" });
            i.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\b.png" });
            p.Items.Add(i);
            return () => i.Items.Move(0, 1);
        });
        yield return new("MediaGroup.MediaItem.SourceMediaFilePath", p =>
        {
            var i = new MediaGroupItemInstance(p);
            var m = new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" };
            i.Items.Add(m);
            p.Items.Add(i);
            return () => m.SourceMediaFilePath = @"C:\other.png";
        });
        yield return new("MediaGroup.MediaItem.Meta.Text", p =>
        {
            var i = new MediaGroupItemInstance(p);
            var m = new MediaGroupItem.MediaItem { SourceMediaFilePath = @"C:\a.png" };
            i.Items.Add(m);
            p.Items.Add(i);
            return () => m.Meta!.Text = "caption";
        });
        yield return new("MediaGroup.AutoAdvanceTimer.IsEnabled", p =>
        {
            var i = new MediaGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.AutoAdvanceTimer.IsEnabled = true;
        });
        yield return new("MediaGroup.AutoAdvanceTimer.IntervalMs", p =>
        {
            var i = new MediaGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.AutoAdvanceTimer.IntervalMs = 9_000;
        });
        // MediaGroupItem.IsLooping is deliberately not covered: it is vestigial (no UI binds it and the
        // playlist's looping logic reads the older SlidesGroupItem.IsLooping), and SerializeItem doesn't copy it.

        // ---- file / document backed groups ----
        yield return new("PDF.Title", p =>
        {
            var i = new PDFSlidesGroupItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("PDF.SourcePresentationFile", p =>
        {
            var i = new PDFSlidesGroupItemInstance(p) { SourcePresentationFile = @"C:\a.pdf" };
            p.Items.Add(i);
            return () => i.SourcePresentationFile = @"C:\b.pdf";
        });
        yield return new("PDF.AutoAdvanceTimer.IsEnabled", p =>
        {
            var i = new PDFSlidesGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.AutoAdvanceTimer.IsEnabled = true;
        });
        yield return new("PowerPoint.Title", p =>
        {
            var i = new PowerPointPresentationItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("PowerPoint.SourcePresentationFile", p =>
        {
            var i = new PowerPointPresentationItemInstance(p) { SourcePresentationFile = @"C:\a.pptx" };
            p.Items.Add(i);
            return () => i.SourcePresentationFile = @"C:\b.pptx";
        });
        yield return new("PowerPoint.AutoAdvanceTimer.IsEnabled", p =>
        {
            var i = new PowerPointPresentationItemInstance(p);
            p.Items.Add(i);
            return () => i.AutoAdvanceTimer.IsEnabled = true;
        });
        yield return new("GoogleSlides.Title", p =>
        {
            var i = new GoogleSlidesGroupItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("GoogleSlides.SourceGooglePresentationId", p =>
        {
            var i = new GoogleSlidesGroupItemInstance(p) { SourceGooglePresentationId = "id1" };
            p.Items.Add(i);
            return () => i.SourceGooglePresentationId = "id2";
        });
        yield return new("GoogleSlides.AutoAdvanceTimer.IsEnabled", p =>
        {
            var i = new GoogleSlidesGroupItemInstance(p);
            p.Items.Add(i);
            return () => i.AutoAdvanceTimer.IsEnabled = true;
        });
        yield return new("OnlineVideo.Title", p =>
        {
            var i = new OnlineVideoItemInstance(p) { Title = "a" };
            p.Items.Add(i);
            return () => i.Title = "b";
        });
        yield return new("OnlineVideo.SourceVideoUrl", p =>
        {
            var i = new OnlineVideoItemInstance(p) { SourceVideoUrl = "https://example.com/a" };
            p.Items.Add(i);
            return () => i.SourceVideoUrl = "https://example.com/b";
        });
    }

    [TestMethod]
    public void EveryPersistedProperty_RaisesChangeAndAltersSerializedDocument()
    {
        var gaps = new List<string>();

        foreach (var c in AllCases())
        {
            try
            {
                var playlist = new PlaylistInstance
                {
                    Title = "p",
                    PlaylistFilePath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".xml")
                };
                var edit = c.Arrange(playlist);
                PlaylistDocumentService.SaveDocument(playlist); // baseline: clean
                Assert.IsFalse(playlist.IsDirty, "precondition: clean after save");

                edit();

                var eventRaised = playlist.IsDirty;
                playlist.IsDirty = false; // discard the event's verdict; ask the tracker
                var serializedChanged = playlist.DirtyTracker.Refresh();

                if (!serializedChanged) gaps.Add($"NOT PERSISTED  {c.Name} (edit did not change the serialized document)");
                else if (!eventRaised) gaps.Add($"NO EVENT       {c.Name} (persisted, but no change event raised)");
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null) root = root.InnerException;
                gaps.Add($"THREW          {c.Name}: {root.GetType().Name}: {root.Message}");
            }
        }

        Assert.AreEqual(0, gaps.Count, "Dirty-tracking gaps:\n" + string.Join("\n", gaps));
    }
}
