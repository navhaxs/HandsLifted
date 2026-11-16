using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>The theme designer's undo history (one per window): the stack and the recorder that feeds it.</summary>
    public sealed class ThemeEditHistory : IDisposable
    {
        private readonly ThemeEditRecorder _recorder;

        internal ThemeEditHistory(ObservableCollection<BaseSlideTheme> themes)
        {
            Themes = themes;
            Stack = new UndoStack();
            _recorder = new ThemeEditRecorder(Stack, themes);
        }

        public UndoStack Stack { get; }
        public ObservableCollection<BaseSlideTheme> Themes { get; }

        public void Dispose() => _recorder.Dispose();
    }

    /// <summary>
    /// Keeps the theme designer's history for as long as its <em>window</em> stays open. The designer control can
    /// be taken off screen and put back (switching tabs in the main window's designer view, say), which must not
    /// lose the history, so it is held here rather than in the control.
    /// </summary>
    public static class ThemeEditHistories
    {
        private static readonly ConditionalWeakTable<TopLevel, ThemeEditHistory> Windows = new();

        public static ThemeEditHistory Get(TopLevel window, ObservableCollection<BaseSlideTheme> themes)
        {
            if (Windows.TryGetValue(window, out var existing))
            {
                if (ReferenceEquals(existing.Themes, themes)) return existing;

                // the theme collection itself was swapped: the old history no longer applies
                existing.Dispose();
                Windows.Remove(window);
            }
            else
            {
                window.Closed += (_, _) =>
                {
                    if (Windows.TryGetValue(window, out var h)) h.Dispose();
                    Windows.Remove(window);
                };
            }

            var history = new ThemeEditHistory(themes);
            Windows.Add(window, history);
            return history;
        }
    }
}
