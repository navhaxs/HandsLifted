using System;
using System.Threading.Tasks;
using HandsLiftedApp.Tests.TestSupport;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Data.Models.Types;
using HandsLiftedApp.Data.SlideTheme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class ThemeEditRecorderTests
{
    private static (ObservableCollection<BaseSlideTheme> Themes, UndoStack Stack, ThemeEditRecorder Recorder) Open(
        Func<DateTime>? now = null)
    {
        var themes = new ObservableCollection<BaseSlideTheme>
        {
            new() { Name = "Default", FontSize = 100 },
            new() { Name = "Worship", Type = SlideThemeType.SongTheme, FontSize = 90 },
        };
        var stack = now == null ? new UndoStack() : new UndoStack(now, TimeSpan.FromSeconds(1.5));
        var recorder = new ThemeEditRecorder(stack, themes);
        return (themes, stack, recorder);
    }

    // ---------------- a theme's settings ----------------

    [TestMethod]
    public Task ChangingASetting_IsUndoable_AndNamedSo() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[1].FontSize = 120;

        Assert.AreEqual("Undo Change Font", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(90, themes[1].FontSize);
        stack.Redo();
        Assert.AreEqual(120, themes[1].FontSize);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DraggingASlider_IsOneStep() => DispatcherTestThread.Run(() =>
    {
        var clock = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var (themes, stack, _) = Open(() => clock);

        for (var size = 91; size <= 140; size++)
        {
            themes[1].FontSize = size;
            clock = clock.AddMilliseconds(16);
        }

        stack.Undo();
        Assert.AreEqual(90, themes[1].FontSize);
        Assert.IsFalse(stack.CanUndo);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task LabelsFollowWhatChanged() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();

        themes[0].Name = "Renamed";
        Assert.AreEqual("Undo Rename Theme", stack.UndoMenuText);
        themes[0].TextAvaloniaColour = Colors.Red;
        Assert.AreEqual("Undo Change Colour", stack.UndoMenuText);
        themes[0].TextAlignment = TextAlignment.Left;
        Assert.AreEqual("Undo Change Alignment", stack.UndoMenuText);
        themes[0].BackgroundGraphicFilePath = "bg.png";
        Assert.AreEqual("Undo Change Background", stack.UndoMenuText);
        themes[0].DropShadowBlurRadius = 3;
        Assert.AreEqual("Undo Change Shadow", stack.UndoMenuText);
        themes[0].Type = SlideThemeType.ScriptureTheme;
        Assert.AreEqual("Undo Move Theme", stack.UndoMenuText);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task UndoRestoresEverySetting_IncludingAFontWeightThatIsNotNormalOrBold() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[0].FontWeight = (XmlFontWeight)FontWeight.Light;
        themes[0].FontStyle = FontStyle.Oblique;
        stack.Clear();

        themes[0].FontSize = 55;
        stack.Undo();
        Assert.AreEqual((int)FontWeight.Light, (int)themes[0].FontWeight);
        Assert.AreEqual(FontStyle.Oblique, themes[0].FontStyle);

        stack.Redo();
        Assert.AreEqual((int)FontWeight.Light, (int)themes[0].FontWeight);
        Assert.AreEqual(55, themes[0].FontSize);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task EditsToDifferentThemes_AreSeparateSteps_InOrder() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[0].FontSize = 10;
        themes[1].FontSize = 20;

        stack.Undo();
        Assert.AreEqual(90, themes[1].FontSize);
        Assert.AreEqual(10, themes[0].FontSize);
        stack.Undo();
        Assert.AreEqual(100, themes[0].FontSize);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task UndoItself_IsNotRecorded_AndLaterEditsStillDiffCorrectly() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[0].FontSize = 10;
        stack.Undo();
        Assert.IsFalse(stack.CanUndo);

        themes[0].FontSize = 11;
        stack.Undo();
        Assert.AreEqual(100, themes[0].FontSize);
        return Task.CompletedTask;
    });

    // ---------------- adding and removing themes ----------------

    [TestMethod]
    public Task AddingATheme_UndoRemovesIt_RedoBringsItBackWithTheSameIdAndSettings() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        var added = new BaseSlideTheme { Name = "Fresh", FontSize = 77, Type = SlideThemeType.ScriptureTheme };
        themes.Add(added);

        Assert.AreEqual("Undo Add Theme", stack.UndoMenuText);
        stack.Undo();
        Assert.AreEqual(2, themes.Count);

        stack.Redo();
        Assert.AreEqual(3, themes.Count);
        Assert.AreEqual(added.Id, themes[2].Id);
        Assert.AreEqual("Fresh", themes[2].Name);
        Assert.AreEqual(77, themes[2].FontSize);
        Assert.AreEqual(SlideThemeType.ScriptureTheme, themes[2].Type);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task RemovingATheme_UndoRestoresItAtTheSamePlaceWithTheSameId() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[1].FontWeight = (XmlFontWeight)FontWeight.SemiBold;
        stack.Clear();
        var id = themes[1].Id;

        themes.RemoveAt(1);
        Assert.AreEqual("Undo Delete Theme", stack.UndoMenuText);

        stack.Undo();
        Assert.AreEqual(2, themes.Count);
        Assert.AreEqual(id, themes[1].Id);
        Assert.AreEqual("Worship", themes[1].Name);
        Assert.AreEqual(90, themes[1].FontSize);
        Assert.AreEqual((int)FontWeight.SemiBold, (int)themes[1].FontWeight);

        stack.Redo();
        Assert.AreEqual(1, themes.Count);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task EditsMadeBeforeADelete_AreWhatComesBack() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[1].FontSize = 150; // edit...
        themes.RemoveAt(1); //       ...then delete

        stack.Undo(); // restore: must have the edited size
        Assert.AreEqual(150, themes[1].FontSize);
        stack.Undo(); // undo the edit on the restored theme
        Assert.AreEqual(90, themes[1].FontSize);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task EditingARestoredTheme_IsRecordedAgain() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes.RemoveAt(1);
        stack.Undo();
        stack.Clear();

        themes[1].Name = "After restore";
        Assert.IsTrue(stack.CanUndo);
        stack.Undo();
        Assert.AreEqual("Worship", themes[1].Name);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ResettingTheCollection_ClearsTheHistory() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, _) = Open();
        themes[0].FontSize = 10;
        Assert.IsTrue(stack.CanUndo);

        themes.Clear(); // the library re-initialises with another folder
        Assert.IsFalse(stack.CanUndo);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Dispose_StopsRecording() => DispatcherTestThread.Run(() =>
    {
        var (themes, stack, recorder) = Open();
        recorder.Dispose();

        themes[0].FontSize = 10;
        themes.Add(new BaseSlideTheme());
        Assert.IsFalse(stack.CanUndo);
        return Task.CompletedTask;
    });

    // ---------------- "Set as default" ----------------

    [TestMethod]
    public Task SetDefaultTheme_UndoRestoresTheAppDefaultAndThePlaylistOverride() => DispatcherTestThread.Run(() =>
    {
        var prefs = new AppPreferencesViewModel();
        var playlist = new PlaylistInstance();
        var oldDefault = Guid.NewGuid();
        var oldOverride = Guid.NewGuid();
        var chosen = Guid.NewGuid();
        prefs.DefaultSongThemeId = oldDefault;
        playlist.DefaultSongThemeId = oldOverride;
        playlist.UndoStack.Clear();

        var saves = 0;
        var stack = new UndoStack();
        stack.Execute(new SetDefaultThemeAction(prefs, playlist, DefaultThemeKind.Song, chosen, () => saves++));

        Assert.AreEqual(chosen, prefs.DefaultSongThemeId);
        Assert.IsNull(playlist.DefaultSongThemeId, "setting the default clears the playlist's own override");
        Assert.IsFalse(playlist.UndoStack.CanUndo, "that belongs to the designer's history, not the playlist's");

        stack.Undo();
        Assert.AreEqual(oldDefault, prefs.DefaultSongThemeId);
        Assert.AreEqual(oldOverride, playlist.DefaultSongThemeId);
        Assert.IsFalse(playlist.UndoStack.CanUndo);

        stack.Redo();
        Assert.AreEqual(chosen, prefs.DefaultSongThemeId);
        Assert.IsNull(playlist.DefaultSongThemeId);
        Assert.AreEqual(3, saves, "each change is saved to the preferences straight away");
        return Task.CompletedTask;
    });

    [DataTestMethod]
    [DataRow(DefaultThemeKind.Song)]
    [DataRow(DefaultThemeKind.SongMotionBackground)]
    [DataRow(DefaultThemeKind.Scripture)]
    public Task SetDefaultTheme_WorksForEachKind(DefaultThemeKind kind) => DispatcherTestThread.Run(() =>
    {
        var prefs = new AppPreferencesViewModel();
        var playlist = new PlaylistInstance();
        var chosen = Guid.NewGuid();
        var stack = new UndoStack();

        stack.Execute(new SetDefaultThemeAction(prefs, playlist, kind, chosen, () => { }));
        var applied = kind switch
        {
            DefaultThemeKind.Song => prefs.DefaultSongThemeId,
            DefaultThemeKind.SongMotionBackground => prefs.DefaultSongMotionThemeId,
            _ => prefs.DefaultScriptureThemeId,
        };
        Assert.AreEqual(chosen, applied);

        stack.Undo();
        Assert.IsNull(kind switch
        {
            DefaultThemeKind.Song => prefs.DefaultSongThemeId,
            DefaultThemeKind.SongMotionBackground => prefs.DefaultSongMotionThemeId,
            _ => prefs.DefaultScriptureThemeId,
        });
        return Task.CompletedTask;
    });
}
