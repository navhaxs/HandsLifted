using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Controls;

[TestClass]
public class ThemePickerItemsTests
{
    private static BaseSlideTheme Theme(string name, SlideThemeType type) => new() { Name = name, Type = type };

    private static Task OnDispatcher(Action body) => DispatcherTestThread.Run(() =>
    {
        body();
        return Task.CompletedTask;
    });

    /// <summary>"#Title" for a header, "(blank)" for the null option, otherwise the theme's name.</summary>
    private static string Describe(object? item) => item switch
    {
        null => "(blank)",
        ThemePickerGroupHeader h => "#" + h.Title,
        BaseSlideTheme t => t.Name,
        _ => "?" + item,
    };

    /// <summary>Every item, in list order.</summary>
    private static string All(ThemePickerItems items) => string.Join(", ", items.Cast<object?>().Select(Describe));

    /// <summary>Only what the dropdown shows (themes of the other kind stay in the list, hidden).</summary>
    private static string Shown(ThemePickerItems items) =>
        string.Join(", ", items.Cast<object?>().Where(items.IsShown).Select(Describe));

    /// <summary>
    /// A copy of the list kept up to date only from CollectionChanged. If the events are wrong (bad index, missing
    /// event, wrong item) the mirror drifts from the real list, which <see cref="AssertMirrors"/> catches.
    /// </summary>
    private sealed class Mirror
    {
        private readonly ThemePickerItems _items;
        public readonly List<object?> Copy;
        public readonly List<NotifyCollectionChangedEventArgs> Events = new();

        public Mirror(ThemePickerItems items)
        {
            _items = items;
            Copy = items.Cast<object?>().ToList();
            items.CollectionChanged += (sender, e) =>
            {
                Assert.AreSame(items, sender);
                Events.Add(e);
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        Copy.InsertRange(e.NewStartingIndex, e.NewItems!.Cast<object?>());
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        for (var i = 0; i < e.OldItems!.Count; i++)
                            Assert.AreSame(e.OldItems[i], Copy[e.OldStartingIndex + i], "Remove event names the wrong item");
                        Copy.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        var moved = Copy[e.OldStartingIndex];
                        Assert.AreSame(e.OldItems![0], moved, "Move event names the wrong item");
                        Copy.RemoveAt(e.OldStartingIndex);
                        Copy.Insert(e.NewStartingIndex, moved);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        Copy[e.NewStartingIndex] = e.NewItems![0];
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Copy.Clear();
                        Copy.AddRange(_items.Cast<object?>());
                        break;
                }
            };
        }

        public void AssertMirrors()
        {
            CollectionAssert.AreEqual(_items.Cast<object?>().ToList(), Copy, "CollectionChanged events do not reproduce the list");
            Assert.AreEqual(Copy.Count, _items.Count);
        }

        /// <summary>True if <paramref name="item"/> was ever taken out of the list (a ComboBox drops its selection then).</summary>
        public bool WasRemoved(object item) => Events.Any(e =>
            (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems!.Cast<object?>().Any(o => ReferenceEquals(o, item))) ||
            (e.Action == NotifyCollectionChangedAction.Move && ReferenceEquals(e.OldItems![0], item)) ||
            e.Action is NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Replace);
    }

    [TestMethod]
    public Task MixedList_SongPicker_GroupsGeneralThenSongThemes_InSourceOrder() => OnDispatcher(() =>
    {
        var source = new ObservableCollection<BaseSlideTheme>
        {
            Theme("s1", SlideThemeType.SongTheme),
            Theme("g1", SlideThemeType.General),
            Theme("sc1", SlideThemeType.ScriptureTheme),
            Theme("s2", SlideThemeType.SongTheme),
            Theme("g2", SlideThemeType.General),
        };

        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, includeBlankOption: false);

        Assert.AreEqual("#General, g1, g2, #Song Themes, s1, s2", Shown(items));
        // The scripture theme stays in the list (hidden, no header) so a song pointing at it keeps its selection.
        Assert.AreEqual("#General, g1, g2, #Song Themes, s1, s2, sc1", All(items));
        Assert.IsFalse(items.IsShown(source[2]));
    });

    [TestMethod]
    public Task MixedList_ScripturePicker_BlankFirst_ThenGeneralThenScriptureThemes() => OnDispatcher(() =>
    {
        var source = new ObservableCollection<BaseSlideTheme>
        {
            Theme("sc1", SlideThemeType.ScriptureTheme),
            Theme("s1", SlideThemeType.SongTheme),
            Theme("g1", SlideThemeType.General),
            Theme("sc2", SlideThemeType.ScriptureTheme),
        };

        var items = new ThemePickerItems(source, ThemePickerTarget.Scripture, includeBlankOption: true);

        Assert.AreEqual("(blank), #General, g1, #Scripture Themes, sc1, sc2", Shown(items));
        Assert.AreEqual("(blank), #General, g1, s1, #Scripture Themes, sc1, sc2", All(items));
        Assert.IsNull(items[0], "the blank option comes first");
        Assert.IsTrue(items.IsShown(null));
    });

    [TestMethod]
    public Task HeadersOnlyForGroupsWithShownThemes() => OnDispatcher(() =>
    {
        var source = new ObservableCollection<BaseSlideTheme>
        {
            Theme("s1", SlideThemeType.SongTheme),
            Theme("s2", SlideThemeType.SongTheme),
        };

        Assert.AreEqual("#Song Themes, s1, s2", All(new ThemePickerItems(source, ThemePickerTarget.Songs, false)));
        // A scripture picker over song themes only: nothing shown, so no header at all (not even an empty "General").
        var scripture = new ThemePickerItems(source, ThemePickerTarget.Scripture, true);
        Assert.AreEqual("(blank)", Shown(scripture));
        Assert.AreEqual(0, scripture.OfType<ThemePickerGroupHeader>().Count());
    });

    [TestMethod]
    public Task NullOrEmptySource() => OnDispatcher(() =>
    {
        Assert.AreEqual(0, new ThemePickerItems(null, ThemePickerTarget.Songs, false).Count);
        Assert.AreEqual("(blank)", All(new ThemePickerItems(null, ThemePickerTarget.Scripture, true)));
        Assert.AreEqual(0, new ThemePickerItems(new ObservableCollection<BaseSlideTheme>(), ThemePickerTarget.Songs, false).Count);
        // A plain (non-observable) list works too, as a snapshot.
        Assert.AreEqual("#General, g", All(new ThemePickerItems(new List<BaseSlideTheme> { Theme("g", SlideThemeType.General) }, ThemePickerTarget.Songs, false)));
    });

    [TestMethod]
    public Task Add_InsertsIntoItsGroup_AndAddsTheHeaderOfANewGroup() => OnDispatcher(() =>
    {
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var source = new ObservableCollection<BaseSlideTheme> { s1 };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false);
        var mirror = new Mirror(items);

        source.Add(Theme("g1", SlideThemeType.General));
        Assert.AreEqual("#General, g1, #Song Themes, s1", All(items));
        mirror.AssertMirrors();
        Assert.AreEqual(2, mirror.Events.Count, "one Add for the new header, one for the theme");
        Assert.IsTrue(mirror.Events.All(e => e.Action == NotifyCollectionChangedAction.Add));

        source.Insert(0, Theme("s0", SlideThemeType.SongTheme));
        Assert.AreEqual("#General, g1, #Song Themes, s0, s1", All(items));
        mirror.AssertMirrors();
        Assert.IsFalse(mirror.WasRemoved(s1), "adding a theme must not take an existing one out of the list");
    });

    [TestMethod]
    public Task Remove_DropsTheTheme_AndTheHeaderOfAnEmptiedGroup() => OnDispatcher(() =>
    {
        var g1 = Theme("g1", SlideThemeType.General);
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var source = new ObservableCollection<BaseSlideTheme> { g1, s1 };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false);
        var mirror = new Mirror(items);

        source.Remove(g1);

        Assert.AreEqual("#Song Themes, s1", All(items));
        mirror.AssertMirrors();
        Assert.AreEqual(2, mirror.Events.Count);
        Assert.IsTrue(mirror.Events.All(e => e.Action == NotifyCollectionChangedAction.Remove));
    });

    [TestMethod]
    public Task Reorder_FollowsTheSourceOrderWithinAGroup() => OnDispatcher(() =>
    {
        var source = new ObservableCollection<BaseSlideTheme>
        {
            Theme("s1", SlideThemeType.SongTheme),
            Theme("g1", SlideThemeType.General),
            Theme("s2", SlideThemeType.SongTheme),
        };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false);
        var mirror = new Mirror(items);

        source.Move(2, 0);

        Assert.AreEqual("#General, g1, #Song Themes, s2, s1", All(items));
        mirror.AssertMirrors();
    });

    [TestMethod]
    public Task TypeChange_MovesTheThemeToItsNewGroup() => OnDispatcher(() =>
    {
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var s2 = Theme("s2", SlideThemeType.SongTheme);
        var g1 = Theme("g1", SlideThemeType.General);
        var source = new ObservableCollection<BaseSlideTheme> { g1, s1, s2 };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false);
        var mirror = new Mirror(items);

        s2.Type = SlideThemeType.General;
        Assert.AreEqual("#General, g1, s2, #Song Themes, s1", All(items));
        mirror.AssertMirrors();

        // Into a group this picker hides: still listed (hidden), and the emptied song group loses its header.
        s1.Type = SlideThemeType.ScriptureTheme;
        Assert.AreEqual("#General, g1, s2, s1", All(items));
        Assert.AreEqual("#General, g1, s2", Shown(items));
        mirror.AssertMirrors();

        // Back again: the header comes back.
        s1.Type = SlideThemeType.SongTheme;
        Assert.AreEqual("#General, g1, s2, #Song Themes, s1", All(items));
        mirror.AssertMirrors();

        // A change to anything but the type does not touch the list.
        var before = mirror.Events.Count;
        s1.Name = "renamed";
        Assert.AreEqual(before, mirror.Events.Count);
    });

    /// <summary>
    /// A ComboBox drops its selection (and its two-way binding writes null back, clearing an item's theme) when the
    /// selected item is removed or moved. So a regroup must move the other items around the one kept in place.
    /// </summary>
    [TestMethod]
    public Task TypeChange_NeverTakesTheKeptItemOutOfTheList() => OnDispatcher(() =>
    {
        var g1 = Theme("g1", SlideThemeType.General);
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var s2 = Theme("s2", SlideThemeType.SongTheme);
        var s3 = Theme("s3", SlideThemeType.SongTheme);
        var source = new ObservableCollection<BaseSlideTheme> { g1, s1, s2, s3 };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false) { KeepInPlace = s2 };
        var mirror = new Mirror(items);

        s2.Type = SlideThemeType.General; // up into the General group
        Assert.AreEqual("#General, g1, s2, #Song Themes, s1, s3", All(items));
        mirror.AssertMirrors();

        s2.Type = SlideThemeType.ScriptureTheme; // down, past the song group, into the hidden scripture group
        Assert.AreEqual("#General, g1, #Song Themes, s1, s3, s2", All(items));
        mirror.AssertMirrors();

        g1.Type = SlideThemeType.SongTheme; // a different theme moves past the kept one
        Assert.AreEqual("#Song Themes, g1, s1, s3, s2", All(items));
        mirror.AssertMirrors();

        source.Move(3, 0); // a source reorder: s3 to the front, so it moves past others
        mirror.AssertMirrors();
        Assert.AreEqual("#Song Themes, s3, g1, s1, s2", All(items));

        source.Move(2, 0); // source is now s1, s3, g1, s2
        source.Move(0, 3); // source is now s3, g1, s2, s1
        s2.Type = SlideThemeType.SongTheme; // back into the shown song group, where source order puts it before s1
        mirror.AssertMirrors();
        Assert.AreEqual("#Song Themes, s3, g1, s2, s1", All(items));

        Assert.IsFalse(mirror.WasRemoved(s2), "the kept item was taken out of the list");
    });

    [TestMethod]
    public Task SourceReset_RebuildsTheList_WithoutTakingOutTheKeptItem() => OnDispatcher(() =>
    {
        var g1 = Theme("g1", SlideThemeType.General);
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var s2 = Theme("s2", SlideThemeType.SongTheme);
        var source = new ResettableCollection { s1, g1, s2 };
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false) { KeepInPlace = s2 };
        var mirror = new Mirror(items);

        var g2 = Theme("g2", SlideThemeType.General);
        source.ReplaceAll(new[] { s2, g2, g1 }); // one Reset: s1 gone, g2 new, everything reordered

        Assert.AreEqual("#General, g2, g1, #Song Themes, s2", All(items));
        mirror.AssertMirrors();
        Assert.IsFalse(mirror.WasRemoved(s2));

        // Clear() also raises Reset.
        source.Clear();
        Assert.AreEqual(0, items.Count);
        mirror.AssertMirrors();

        // The themes that left are no longer watched.
        var before = mirror.Events.Count;
        s1.Type = SlideThemeType.General;
        g1.Type = SlideThemeType.SongTheme;
        Assert.AreEqual(before, mirror.Events.Count, "a removed theme's type change still reached the list");
    });

    [TestMethod]
    public Task SourceAndTargetCanChange() => OnDispatcher(() =>
    {
        var g1 = Theme("g1", SlideThemeType.General);
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var sc1 = Theme("sc1", SlideThemeType.ScriptureTheme);
        var first = new ObservableCollection<BaseSlideTheme> { g1, s1 };
        var items = new ThemePickerItems(first, ThemePickerTarget.Songs, false);
        var mirror = new Mirror(items);

        var second = new ObservableCollection<BaseSlideTheme> { sc1, g1 };
        items.Source = second;
        Assert.AreEqual("#General, g1, sc1", All(items));
        mirror.AssertMirrors();

        first.Add(Theme("ignored", SlideThemeType.General));
        Assert.AreEqual("#General, g1, sc1", All(items), "the old source is no longer watched");

        items.Target = ThemePickerTarget.Scripture;
        Assert.AreEqual("#General, g1, #Scripture Themes, sc1", All(items));
        mirror.AssertMirrors();

        items.IncludeBlankOption = true;
        Assert.AreEqual("(blank), #General, g1, #Scripture Themes, sc1", All(items));
        mirror.AssertMirrors();

        items.Source = null;
        Assert.AreEqual("(blank)", All(items));
        mirror.AssertMirrors();
    });

    [TestMethod]
    public Task Resynced_IsRaisedAfterATypeChange() => OnDispatcher(() =>
    {
        var s1 = Theme("s1", SlideThemeType.SongTheme);
        var items = new ThemePickerItems(new ObservableCollection<BaseSlideTheme> { s1 }, ThemePickerTarget.Songs, false);
        var raised = 0;
        items.Resynced += (_, _) => raised++;

        s1.Type = SlideThemeType.ScriptureTheme;

        Assert.AreEqual(1, raised);
    });

    // The playlist's theme collection and its themes live as long as the app; a picker list is made per dropdown.
    [TestMethod]
    public Task AnUnreferencedList_CanBeCollected_WhileTheSourceLives() => OnDispatcher(() =>
    {
        var theme = Theme("s1", SlideThemeType.SongTheme);
        var source = new ObservableCollection<BaseSlideTheme> { theme };

        var weak = CreateUnreferencedList(source);
        for (var i = 0; i < 10 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.IsFalse(weak.IsAlive, "the source collection (or a theme) keeps the picker list alive");
        // The source still works after the list has gone.
        source.Add(Theme("s2", SlideThemeType.SongTheme));
        theme.Type = SlideThemeType.General;
        GC.KeepAlive(source);
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateUnreferencedList(ObservableCollection<BaseSlideTheme> source)
    {
        var items = new ThemePickerItems(source, ThemePickerTarget.Songs, false);
        Assert.AreEqual(2, items.Count);
        return new WeakReference(items);
    }

    [TestMethod]
    public Task ReadOnlyIList() => OnDispatcher(() =>
    {
        var items = new ThemePickerItems(new ObservableCollection<BaseSlideTheme> { Theme("g", SlideThemeType.General) }, ThemePickerTarget.Songs, false);
        IList list = items;
        Assert.IsTrue(list.IsReadOnly);
        Assert.AreEqual(1, list.IndexOf(list[1]));
        Assert.IsTrue(list.Contains(list[0]));
        Assert.ThrowsException<NotSupportedException>(() => list.Add(null));
    });

    /// <summary>Raises a single Reset for a whole replacement, like a library reload does.</summary>
    private sealed class ResettableCollection : ObservableCollection<BaseSlideTheme>
    {
        public void ReplaceAll(IEnumerable<BaseSlideTheme> themes)
        {
            Items.Clear();
            foreach (var t in themes) Items.Add(t);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
