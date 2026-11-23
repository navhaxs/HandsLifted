using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Utilities;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Controls
{
    /// <summary>Which kind of item a theme picker chooses a theme for (decides which themes it shows).</summary>
    public enum ThemePickerTarget
    {
        Songs,
        Scripture,
    }

    /// <summary>A section label row in a theme picker ("General", "Song Themes", ...). Never selectable.</summary>
    public sealed class ThemePickerGroupHeader
    {
        internal ThemePickerGroupHeader(SlideThemeType type)
        {
            Type = type;
            Title = ThemePickerItems.GroupTitle(type);
        }

        public SlideThemeType Type { get; }
        public string Title { get; }

        public override string ToString() => Title;
    }

    /// <summary>
    /// The items of a theme dropdown, grouped by <see cref="BaseSlideTheme.Type"/>: an optional blank (null) option
    /// first, then per group (General, Song Themes, Scripture Themes) a <see cref="ThemePickerGroupHeader"/> followed
    /// by that group's themes in source order. Kept in step with the source collection and with each theme's Type.
    /// <para>
    /// Every source theme stays in the list, also those the target does not use (<see cref="IsShown"/> is false for
    /// them, and the picker hides their rows): a ComboBox drops its selection - and its two-way binding writes null
    /// back, clearing an item's theme - when the selected item is not in its list. That is also why a regroup never
    /// takes <see cref="KeepInPlace"/> (the picker's selection) out of the list: it moves the other items around it.
    /// Headers are only added for groups the target shows.
    /// </para>
    /// <para>
    /// The source and the themes are long-lived (the playlist's theme library); a list is made per dropdown. So it
    /// listens to them through Avalonia's weak events: a list nobody references any more can be collected.
    /// Must be used on the UI thread (like the source collection it mirrors).
    /// </para>
    /// </summary>
    public sealed class ThemePickerItems : IList, IReadOnlyList<object?>, INotifyCollectionChanged,
        IWeakEventSubscriber<NotifyCollectionChangedEventArgs>, IWeakEventSubscriber<PropertyChangedEventArgs>
    {
        /// <summary>Group order, top to bottom (same order as the theme designer's lists).</summary>
        public static readonly IReadOnlyList<SlideThemeType> GroupOrder =
            new[] { SlideThemeType.General, SlideThemeType.SongTheme, SlideThemeType.ScriptureTheme };

        /// <summary>The header text of a group (same wording as the theme designer's section headers).</summary>
        public static string GroupTitle(SlideThemeType type) => type switch
        {
            SlideThemeType.General => "General",
            SlideThemeType.SongTheme => "Song Themes",
            SlideThemeType.ScriptureTheme => "Scripture Themes",
            _ => type.ToString(),
        };

        /// <summary>Whether a picker for <paramref name="target"/> offers <paramref name="theme"/> (the same rule as before grouping).</summary>
        public static bool Applies(BaseSlideTheme theme, ThemePickerTarget target) =>
            target == ThemePickerTarget.Scripture ? theme.AppliesToScripture() : theme.AppliesToSongs();

        private readonly List<object?> _items = new();
        private readonly Dictionary<SlideThemeType, ThemePickerGroupHeader> _headers = new();
        private readonly HashSet<BaseSlideTheme> _watchedThemes = new(ReferenceEqualityComparer.Instance);
        private IEnumerable<BaseSlideTheme>? _source;
        private INotifyCollectionChanged? _watchedSource;
        private ThemePickerTarget _target;
        private bool _includeBlankOption;

        public ThemePickerItems(IEnumerable<BaseSlideTheme>? source = null, ThemePickerTarget target = ThemePickerTarget.Songs,
            bool includeBlankOption = false)
        {
            _target = target;
            _includeBlankOption = includeBlankOption;
            Source = source;
            if (source == null) Resync();
        }

        /// <summary>The themes to list (usually the playlist's Designs). Watched if it is an <see cref="INotifyCollectionChanged"/>.</summary>
        public IEnumerable<BaseSlideTheme>? Source
        {
            get => _source;
            set
            {
                if (ReferenceEquals(_source, value)) return;
                if (_watchedSource != null) WeakEvents.CollectionChanged.Unsubscribe(_watchedSource, this);
                _source = value;
                _watchedSource = value as INotifyCollectionChanged;
                if (_watchedSource != null) WeakEvents.CollectionChanged.Subscribe(_watchedSource, this);
                Resync();
            }
        }

        public ThemePickerTarget Target
        {
            get => _target;
            set
            {
                if (_target == value) return;
                _target = value;
                Resync();
            }
        }

        /// <summary>Offer a blank (null) option first, outside any group.</summary>
        public bool IncludeBlankOption
        {
            get => _includeBlankOption;
            set
            {
                if (_includeBlankOption == value) return;
                _includeBlankOption = value;
                Resync();
            }
        }

        /// <summary>
        /// The item a regroup must not take out of the list (the picker's selected item). Other items are moved
        /// around it instead. Only a removal from the source (or a change of <see cref="Source"/>) removes it.
        /// </summary>
        public object? KeepInPlace { get; set; }

        /// <summary>Raised after the list was brought up to date (an item's <see cref="IsShown"/> may have changed).</summary>
        public event EventHandler? Resynced;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>Whether the picker shows <paramref name="item"/>: the blank option and headers always, a theme if the target uses it.</summary>
        public bool IsShown(object? item) => item switch
        {
            null => true,
            ThemePickerGroupHeader => true,
            BaseSlideTheme theme => Applies(theme, _target),
            _ => false,
        };

        private ThemePickerGroupHeader Header(SlideThemeType type)
        {
            if (!_headers.TryGetValue(type, out var header)) _headers[type] = header = new ThemePickerGroupHeader(type);
            return header;
        }

        private List<object?> BuildDesired(List<BaseSlideTheme> themes)
        {
            var desired = new List<object?>();
            if (_includeBlankOption) desired.Add(null);
            foreach (var type in GroupOrder)
            {
                var members = themes.Where(t => t.Type == type).ToList();
                if (members.Count == 0) continue;
                if (members.Any(t => Applies(t, _target))) desired.Add(Header(type));
                desired.AddRange(members);
            }

            // A Type outside the known groups (not expected): listed last, shown only if it applies.
            desired.AddRange(themes.Where(t => !GroupOrder.Contains(t.Type)));
            return desired;
        }

        private List<BaseSlideTheme> CurrentThemes()
        {
            var seen = new HashSet<BaseSlideTheme>(ReferenceEqualityComparer.Instance);
            return _source?.Where(t => t != null && seen.Add(t)).ToList() ?? new List<BaseSlideTheme>();
        }

        private void WatchThemes(List<BaseSlideTheme> themes)
        {
            foreach (var gone in _watchedThemes.Where(t => !themes.Contains(t, ReferenceEqualityComparer.Instance)).ToList())
            {
                WeakEvents.ThreadSafePropertyChanged.Unsubscribe(gone, this);
                _watchedThemes.Remove(gone);
            }

            foreach (var theme in themes)
            {
                if (_watchedThemes.Add(theme)) WeakEvents.ThreadSafePropertyChanged.Subscribe(theme, this);
            }
        }

        /// <summary>Brings the list in line with the source, raising one Add/Remove per changed position.</summary>
        private void Resync()
        {
            var themes = CurrentThemes();
            WatchThemes(themes);
            var desired = BuildDesired(themes);
            var wanted = new HashSet<object>(desired.Where(o => o != null)!, ReferenceEqualityComparer.Instance);
            var wantBlank = desired.Count > 0 && desired[0] == null;

            // 1. Drop what is no longer wanted.
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item == null ? !wantBlank : !wanted.Contains(item)) RemoveItemAt(i);
            }

            // 2. Put the rest in order. The kept item never moves: whatever stands in its way is taken out and put
            //    back further down when its turn comes.
            var keep = KeepInPlace != null && wanted.Contains(KeepInPlace) && _items.Contains(KeepInPlace, ReferenceEqualityComparer.Instance)
                ? KeepInPlace
                : null;
            var index = 0;
            while (index < desired.Count)
            {
                var want = desired[index];
                if (index < _items.Count && ReferenceEquals(_items[index], want))
                {
                    index++;
                    continue;
                }

                if (keep != null && ReferenceEquals(want, keep))
                {
                    RemoveItemAt(index); // not the kept item (it is further down); re-inserted on its own turn
                    continue;
                }

                var existing = IndexOf(want, index + 1);
                if (existing >= 0) RemoveItemAt(existing);
                InsertItem(index, want);
                index++;
            }

            while (_items.Count > desired.Count) RemoveItemAt(_items.Count - 1);

            Resynced?.Invoke(this, EventArgs.Empty);
        }

        private int IndexOf(object? item, int start)
        {
            for (var i = start; i < _items.Count; i++)
                if (ReferenceEquals(_items[i], item)) return i;
            return -1;
        }

        private void RemoveItemAt(int index)
        {
            var item = _items[index];
            _items.RemoveAt(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        private void InsertItem(int index, object? item)
        {
            _items.Insert(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        void IWeakEventSubscriber<NotifyCollectionChangedEventArgs>.OnEvent(object? sender, WeakEvent ev, NotifyCollectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _watchedSource)) Resync();
        }

        void IWeakEventSubscriber<PropertyChangedEventArgs>.OnEvent(object? sender, WeakEvent ev, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(BaseSlideTheme.Type)) Resync();
        }

        // Read-only list.
        public object? this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public int Count => _items.Count;
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        public bool Contains(object? value) => IndexOf(value, 0) >= 0;
        public int IndexOf(object? value) => IndexOf(value, 0);
        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public IEnumerator<object?> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }
}
