using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using HandsLiftedApp.Data.Data.Models.Slides;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>One custom slide's undo history inside one editor window.</summary>
    public sealed class CustomSlideEditHistory : IDisposable
    {
        private readonly CustomSlideEditRecorder _recorder;

        internal CustomSlideEditHistory(CustomSlide slide)
        {
            Stack = new UndoStack();
            _recorder = new CustomSlideEditRecorder(Stack, slide);
        }

        public UndoStack Stack { get; }

        public void Dispose() => _recorder.Dispose();
    }

    /// <summary>
    /// Keeps each custom slide's history for as long as the editor <em>window</em> that is showing it stays open.
    ///
    /// The slide editor control itself cannot own the histories: the media group editor rebuilds that control
    /// every time the selected thumbnail changes, so a history kept in the control would vanish when you switch to
    /// another slide and back. The window outlives those switches; closing it drops everything.
    /// </summary>
    public static class CustomSlideEditHistories
    {
        private static readonly ConditionalWeakTable<TopLevel, Dictionary<CustomSlide, CustomSlideEditHistory>> Windows = new();

        public static CustomSlideEditHistory Get(TopLevel window, CustomSlide slide)
        {
            if (!Windows.TryGetValue(window, out var perSlide))
            {
                perSlide = new Dictionary<CustomSlide, CustomSlideEditHistory>();
                Windows.Add(window, perSlide);

                var histories = perSlide;
                window.Closed += (_, _) =>
                {
                    foreach (var h in histories.Values) h.Dispose();
                    histories.Clear();
                    Windows.Remove(window);
                };
            }

            if (!perSlide.TryGetValue(slide, out var history))
            {
                history = new CustomSlideEditHistory(slide);
                perSlide[slide] = history;
            }

            return history;
        }
    }
}
