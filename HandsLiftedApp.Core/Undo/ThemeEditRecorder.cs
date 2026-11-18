using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Undo history for the slide theme designer window.
    ///
    /// Themes are the app-wide library: every theme is its own file, written (debounced) when it changes, created
    /// when a theme is added to the collection and deleted when one is removed. This recorder watches that
    /// collection and every theme in it:
    ///  - a change to a theme's settings becomes one before/after step (compared on the theme's whole saved state,
    ///    so sliders and typing merge while the pauses are short);
    ///  - adding a theme (new, duplicate, import) and removing one are steps too. Undoing a removal puts the theme
    ///    back with the same Id, so everything that referred to it (songs, scripture, defaults) points at it again.
    ///
    /// Undo writes back through the themes themselves, so the library saves, renames and deletes the files as it
    /// would for any edit. Not recorded: changes while an undo/redo is running, or on another thread.
    /// </summary>
    public sealed class ThemeEditRecorder : IDisposable
    {
        private readonly UndoStack _stack;
        private readonly ObservableCollection<BaseSlideTheme> _themes;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly Dictionary<Guid, ThemeSnapshot> _last = new();
        private readonly Dictionary<BaseSlideTheme, PropertyChangedEventHandler> _hooked = new();

        public ThemeEditRecorder(UndoStack stack, ObservableCollection<BaseSlideTheme> themes)
        {
            _stack = stack;
            _themes = themes;
            _themes.CollectionChanged += OnThemesChanged;
            Resync();
        }

        public void Dispose()
        {
            _themes.CollectionChanged -= OnThemesChanged;
            foreach (var (theme, handler) in _hooked) theme.PropertyChanged -= handler;
            _hooked.Clear();
            _last.Clear();
        }

        private void Resync()
        {
            foreach (var (theme, handler) in _hooked.ToList())
            {
                if (_themes.Contains(theme)) continue;
                theme.PropertyChanged -= handler;
                _hooked.Remove(theme);
            }

            _last.Clear();
            foreach (var theme in _themes)
            {
                Hook(theme);
                _last[theme.Id] = ThemeSnapshot.Of(theme);
            }
        }

        private void Hook(BaseSlideTheme theme)
        {
            if (_hooked.ContainsKey(theme)) return;
            PropertyChangedEventHandler handler = (_, e) => OnThemeChanged(theme, e.PropertyName);
            theme.PropertyChanged += handler;
            _hooked[theme] = handler;
        }

        // ---------------- collection: add / remove ----------------

        private void OnThemesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // the library was re-initialised (another themes folder): the old history no longer fits
                Resync();
                _stack.Clear();
                return;
            }

            var record = !_stack.IsApplying && Environment.CurrentManagedThreadId == _ownerThread;
            var steps = new List<IUndoableAction>();

            if (e.OldItems != null)
            {
                var index = e.OldStartingIndex;
                foreach (BaseSlideTheme removed in e.OldItems)
                {
                    if (_hooked.Remove(removed, out var handler)) removed.PropertyChanged -= handler;
                    // the last state we saw is the one the theme had when it was deleted
                    if (record && _last.TryGetValue(removed.Id, out var snapshot))
                        steps.Add(new ThemeStructureAction(_themes, snapshot, index, add: false, "Delete Theme"));
                    _last.Remove(removed.Id);
                    index++;
                }
            }

            if (e.NewItems != null)
            {
                var index = e.NewStartingIndex;
                foreach (BaseSlideTheme added in e.NewItems)
                {
                    Hook(added);
                    var snapshot = ThemeSnapshot.Of(added);
                    _last[added.Id] = snapshot;
                    if (record)
                        steps.Add(new ThemeStructureAction(_themes, snapshot, index, add: true, "Add Theme"));
                    index++;
                }
            }

            foreach (var step in steps) _stack.Record(step);
        }

        // ---------------- a theme's own settings ----------------

        private void OnThemeChanged(BaseSlideTheme theme, string? propertyName)
        {
            if (_stack.IsApplying) return;

            // Derived views of other settings (Calculated*, LineHeight) are recalculated asynchronously, possibly
            // on another thread, and never carry a change of their own; reacting to them could only mis-time the
            // comparison below.
            if (propertyName != null
                && (propertyName.StartsWith("Calculated", StringComparison.Ordinal)
                    || propertyName == nameof(BaseSlideTheme.LineHeight)))
                return;

            var now = ThemeSnapshot.Of(theme);
            _last.TryGetValue(theme.Id, out var old);
            _last[theme.Id] = now;
            if (old == null || old.Equals(now)) return;
            if (Environment.CurrentManagedThreadId != _ownerThread) return;

            _stack.Record(new ThemeEditAction(_themes, theme.Id, old, now, Label(old.Template, now.Template),
                snapshot => _last[theme.Id] = snapshot));
        }

        // Named by what actually differs between the two states, not by which change notification happened to
        // arrive first (a derived property such as LineHeight can notify before the real one).
        private static readonly (string Label, string[] Properties)[] Groups =
        {
            ("Rename Theme", new[] { nameof(BaseSlideTheme.Name) }),
            ("Move Theme", new[] { nameof(BaseSlideTheme.Type) }),
            ("Change Font", new[]
            {
                nameof(BaseSlideTheme.FontFamilyAsText), nameof(BaseSlideTheme.FontWeight),
                nameof(BaseSlideTheme.FontStyle), nameof(BaseSlideTheme.FontSize),
                nameof(BaseSlideTheme.TitleFontSize), nameof(BaseSlideTheme.CopyrightFontSize),
                nameof(BaseSlideTheme.LineHeightEm),
            }),
            ("Change Colour", new[]
            {
                nameof(BaseSlideTheme.TextAvaloniaColour), nameof(BaseSlideTheme.BackgroundAvaloniaColour),
                nameof(BaseSlideTheme.DropShadowAvaloniaColour),
            }),
            ("Change Alignment", new[]
            {
                nameof(BaseSlideTheme.TextAlignment), nameof(BaseSlideTheme.TitleTextAlignment),
                nameof(BaseSlideTheme.CopyrightTextAlignment), nameof(BaseSlideTheme.TitleVerticalAlignment),
            }),
            ("Change Background", new[]
            {
                nameof(BaseSlideTheme.BackgroundMode), nameof(BaseSlideTheme.BackgroundGraphicFilePath),
                nameof(BaseSlideTheme.DefaultMotionBackgroundVideoPath), nameof(BaseSlideTheme.BackgroundOpacity),
            }),
            ("Change Shadow", new[]
            {
                nameof(BaseSlideTheme.DropShadowEnabled), nameof(BaseSlideTheme.DropShadowOffsetX),
                nameof(BaseSlideTheme.DropShadowOffsetY), nameof(BaseSlideTheme.DropShadowBlurRadius),
            }),
            ("Change Autofit", new[]
            {
                nameof(BaseSlideTheme.AutofitEnabled), nameof(BaseSlideTheme.AutofitMinFontSizeRatio),
                nameof(BaseSlideTheme.ScriptureMaxShrinkRatio),
            }),
        };

        private static string Label(BaseSlideTheme before, BaseSlideTheme after)
        {
            var labels = Groups
                .Where(g => g.Properties.Any(p => !Equals(Read(before, p), Read(after, p))))
                .Select(g => g.Label)
                .ToList();
            return labels.Count == 1 ? labels[0] : "Edit Theme";
        }

        private static object? Read(BaseSlideTheme theme, string property) => property switch
        {
            nameof(BaseSlideTheme.FontWeight) => (int)theme.FontWeight, // a wrapper type without value equality
            _ => typeof(BaseSlideTheme).GetProperty(property)!.GetValue(theme),
        };
    }

    /// <summary>A change to one theme's settings.</summary>
    internal sealed class ThemeEditAction : IUndoableAction
    {
        private readonly ObservableCollection<BaseSlideTheme> _themes;
        private readonly Guid _id;
        private readonly ThemeSnapshot _before;
        private ThemeSnapshot _after;
        private readonly Action<ThemeSnapshot> _resync;

        public ThemeEditAction(ObservableCollection<BaseSlideTheme> themes, Guid id, ThemeSnapshot before,
            ThemeSnapshot after, string description, Action<ThemeSnapshot> resync)
        {
            _themes = themes;
            _id = id;
            _before = before;
            _after = after;
            Description = description;
            _resync = resync;
        }

        public string Description { get; }

        public void Do() => Write(_after);
        public void Undo() => Write(_before);

        private void Write(ThemeSnapshot snapshot)
        {
            var theme = _themes.FirstOrDefault(t => t.Id == _id)
                        ?? throw new InvalidOperationException($"Theme {_id} is no longer in the library");
            theme.CopyFrom(snapshot.Template);
            // the writes above raised change events; tell the recorder what the theme looks like now
            _resync(ThemeSnapshot.Of(theme));
        }

        public bool TryMerge(IUndoableAction next)
        {
            if (next is not ThemeEditAction n || n._id != _id || n.Description != Description) return false;
            _after = n._after;
            return true;
        }
    }

    /// <summary>A theme added to or removed from the library.</summary>
    internal sealed class ThemeStructureAction : IUndoableAction
    {
        private readonly ObservableCollection<BaseSlideTheme> _themes;
        private readonly ThemeSnapshot _snapshot;
        private readonly int _index;
        private readonly bool _add;

        public ThemeStructureAction(ObservableCollection<BaseSlideTheme> themes, ThemeSnapshot snapshot, int index,
            bool add, string description)
        {
            _themes = themes;
            _snapshot = snapshot;
            _index = index;
            _add = add;
            Description = description;
        }

        public string Description { get; }

        public void Do()
        {
            if (_add) Insert();
            else Remove();
        }

        public void Undo()
        {
            if (_add) Remove();
            else Insert();
        }

        private void Insert()
        {
            // a fresh theme object with the saved settings and the same Id
            var theme = new BaseSlideTheme();
            theme.CopyFrom(_snapshot.Template);
            _themes.Insert(Math.Clamp(_index, 0, _themes.Count), theme);
        }

        private void Remove()
        {
            var theme = _themes.FirstOrDefault(t => t.Id == _snapshot.Template.Id)
                        ?? throw new InvalidOperationException($"Theme {_snapshot.Template.Id} is no longer in the library");
            _themes.Remove(theme);
        }
    }

    /// <summary>A theme's saved settings, with value equality.</summary>
    internal sealed class ThemeSnapshot : IEquatable<ThemeSnapshot>
    {
        private readonly string _key;

        private ThemeSnapshot(BaseSlideTheme template, string key)
        {
            Template = template;
            _key = key;
        }

        /// <summary>A private copy of the theme (never shown anywhere), used both to compare and to restore from.</summary>
        public BaseSlideTheme Template { get; }

        public static ThemeSnapshot Of(BaseSlideTheme theme)
        {
            var copy = new BaseSlideTheme();
            copy.CopyFrom(theme);

            return new ThemeSnapshot(copy, KeyOf(copy));
        }

        // The real settings only: the writable properties and public fields CopyFrom copies, minus the derived
        // "Calculated*" views. Those views update asynchronously, so including them (as serializing the theme to
        // XML would) makes the same settings compare as different depending on timing.
        private static readonly System.Reflection.PropertyInfo[] KeyProperties = typeof(BaseSlideTheme)
            .GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                        && !p.Name.StartsWith("Calculated", StringComparison.Ordinal))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        private static readonly System.Reflection.FieldInfo[] KeyFields = typeof(BaseSlideTheme)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToArray();

        private static string KeyOf(BaseSlideTheme theme)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in KeyProperties) Append(sb, p.Name, p.GetValue(theme));
            foreach (var f in KeyFields) Append(sb, f.Name, f.GetValue(theme));
            return sb.ToString();
        }

        private static void Append(System.Text.StringBuilder sb, string name, object? value)
        {
            // wrapper types whose ToString() says nothing about the value
            var text = value switch
            {
                null => "~",
                HandsLiftedApp.Data.Data.Models.Types.XmlFontFamily family => (string)family,
                HandsLiftedApp.Data.Data.Models.Types.XmlFontWeight weight => ((int)weight).ToString(),
                HandsLiftedApp.Data.Data.Models.Types.XmlColor colour => ((Avalonia.Media.Color)colour).ToString(),
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "~",
            };
            sb.Append(name).Append('=').Append(text.Length).Append(':').Append(text).Append('\u0001');
        }

        public bool Equals(ThemeSnapshot? other) => other != null && _key == other._key;
        public override bool Equals(object? obj) => Equals(obj as ThemeSnapshot);
        public override int GetHashCode() => _key.GetHashCode();
    }

    /// <summary>Which of the three app-wide default themes a "Set as default" changes.</summary>
    public enum DefaultThemeKind
    {
        Song,
        SongMotionBackground,
        Scripture,
    }
}
