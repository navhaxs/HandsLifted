using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Gives the song editor window its own undo history.
    ///
    /// A song is edited in many ways (title/copyright boxes, lyric text boxes, add/delete/reorder sections, the
    /// arrangement list, the theme picker, motion background, free-text import). Rather than routing every one of
    /// those through an action, this watches the song for any change and compares a snapshot of its whole
    /// editable state against the previous snapshot; a difference becomes one "before -> after" step.
    ///
    /// Undo writes the snapshot back through the song instance, exactly as the editor's own controls do, so the
    /// shared library song autosaves as usual. Typing merges into one step while the pauses are short
    /// (see <see cref="UndoStack.MergeWindow"/>). Bursts that are one user action (a free-text import) are grouped
    /// with <see cref="BeginBatch"/>.
    ///
    /// Not recorded: changes made while an undo/redo is running, inside <see cref="Suppress"/>, or on another
    /// thread than the one that created the recorder.
    /// </summary>
    public sealed class SongEditRecorder : IDisposable
    {
        private readonly UndoStack _stack;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly List<SongStanza> _hookedStanzas = new();

        private SongItemInstance? _song;
        private SongItem? _source;
        private ObservableCollection<SongStanza>? _stanzas;
        private ObservableCollection<Guid>? _arrangement;
        private SongSnapshot? _last;
        private int _batchDepth;
        private string? _batchDescription;
        private int _suppressed;

        public SongEditRecorder(UndoStack stack, SongItemInstance? song = null)
        {
            _stack = stack;
            Retarget(song);
        }

        /// <summary>Starts watching a different song (the editor was pointed at another one). Does not touch the stack.</summary>
        public void Retarget(SongItemInstance? song)
        {
            Detach();
            _song = song;
            _source = song?.ResolvedSong;
            if (_source != null) Attach(_source);
            _last = Capture();
        }

        /// <summary>Everything changed inside the returned scope becomes ONE step with the given description.</summary>
        public IDisposable BeginBatch(string description)
        {
            if (_batchDepth == 0) _batchDescription = description;
            _batchDepth++;
            return new Scope(() =>
            {
                if (--_batchDepth > 0) return;
                var d = _batchDescription;
                _batchDescription = null;
                Flush(d);
            });
        }

        /// <summary>Changes made inside the returned scope are not recorded (but the next edit still diffs correctly).</summary>
        public IDisposable Suppress()
        {
            Interlocked.Increment(ref _suppressed);
            return new Scope(() =>
            {
                if (Interlocked.Decrement(ref _suppressed) == 0 && _batchDepth == 0) _last = Capture();
            });
        }

        public void Dispose() => Detach();

        // ---------------- watching ----------------

        private void Attach(SongItem source)
        {
            source.PropertyChanged += OnSourcePropertyChanged;
            HookCollections(source);
        }

        private void Detach()
        {
            if (_source != null) _source.PropertyChanged -= OnSourcePropertyChanged;
            UnhookCollections();
            _source = null;
            _song = null;
        }

        private void HookCollections(SongItem source)
        {
            UnhookCollections();

            _stanzas = source.Stanzas;
            _stanzas.CollectionChanged += OnStanzasCollectionChanged;
            foreach (var s in _stanzas) HookStanza(s);

            _arrangement = source.Arrangement;
            _arrangement.CollectionChanged += OnArrangementChanged;
        }

        private void UnhookCollections()
        {
            if (_stanzas != null) _stanzas.CollectionChanged -= OnStanzasCollectionChanged;
            foreach (var s in _hookedStanzas.ToList()) s.PropertyChanged -= OnStanzaPropertyChanged;
            _hookedStanzas.Clear();
            _stanzas = null;

            if (_arrangement != null) _arrangement.CollectionChanged -= OnArrangementChanged;
            _arrangement = null;
        }

        private void HookStanza(SongStanza s)
        {
            if (_hookedStanzas.Contains(s)) return;
            s.PropertyChanged += OnStanzaPropertyChanged;
            _hookedStanzas.Add(s);
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // the collections themselves were swapped for new ones: follow them
            if (_source != null && (e.PropertyName == nameof(SongItem.Stanzas) || e.PropertyName == nameof(SongItem.Arrangement)))
                HookCollections(_source);
            OnChanged();
        }

        private void OnStanzasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_stanzas != null)
            {
                foreach (var gone in _hookedStanzas.Where(h => !_stanzas.Contains(h)).ToList())
                {
                    gone.PropertyChanged -= OnStanzaPropertyChanged;
                    _hookedStanzas.Remove(gone);
                }

                foreach (var s in _stanzas) HookStanza(s);
            }

            OnChanged();
        }

        private void OnStanzaPropertyChanged(object? sender, PropertyChangedEventArgs e) => OnChanged();
        private void OnArrangementChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnChanged();

        private void OnChanged()
        {
            if (_batchDepth > 0 || _stack.IsApplying) return;
            Flush(null);
        }

        private void Flush(string? forcedDescription)
        {
            if (_song == null) return;

            var now = Capture();
            var old = _last;
            _last = now;
            if (old == null || old.Equals(now)) return;
            if (!ShouldRecord()) return;

            _stack.Record(new SongStateAction(old, now, forcedDescription ?? SongSnapshot.Describe(old, now), Write));
        }

        private bool ShouldRecord() =>
            Volatile.Read(ref _suppressed) == 0
            && !_stack.IsApplying
            && Environment.CurrentManagedThreadId == _ownerThread;

        private SongSnapshot? Capture() => _song == null || _source == null ? null : SongSnapshot.Of(_song);

        // ---------------- undo / redo ----------------

        /// <summary>Writes a snapshot back through the song instance (so the library autosave runs).</summary>
        private void Write(SongSnapshot target)
        {
            var song = _song ?? throw new InvalidOperationException("The song editor is no longer attached to a song");

            if (song.Title != target.Title) song.Title = target.Title;
            if (song.Copyright != target.Copyright) song.Copyright = target.Copyright;
            if (song.Design != target.Design) song.Design = target.Design;
            if (song.MotionBackgroundVideoPath != target.MotionBackgroundVideoPath)
                song.MotionBackgroundVideoPath = target.MotionBackgroundVideoPath;
            if (song.StartOnTitleSlide != target.StartOnTitleSlide) song.StartOnTitleSlide = target.StartOnTitleSlide;
            if (song.EndOnBlankSlide != target.EndOnBlankSlide) song.EndOnBlankSlide = target.EndOnBlankSlide;

            WriteStanzas(song, target);

            // named arrangements: replace the contents in place
            var arrangements = song.Arrangements;
            arrangements.Clear();
            foreach (var (name, ids) in target.Arrangements) arrangements[name] = ids.ToList();

            if (song.SelectedArrangementId != target.SelectedArrangementId)
                song.SelectedArrangementId = target.SelectedArrangementId;

            // the live (selected) arrangement last: it is the one the slides are built from
            var arrangement = song.Arrangement;
            if (!arrangement.SequenceEqual(target.Arrangement))
            {
                arrangement.Clear();
                foreach (var id in target.Arrangement) arrangement.Add(id);
            }

            // the writes above went through the song's own change notifications; resync so they are not mistaken for edits
            _last = Capture();
        }

        private static void WriteStanzas(SongItemInstance song, SongSnapshot target)
        {
            var stanzas = song.Stanzas;
            var keep = target.Stanzas.Select(s => s.Id).ToHashSet();

            for (var i = stanzas.Count - 1; i >= 0; i--)
                if (!keep.Contains(stanzas[i].Id)) stanzas.RemoveAt(i);

            for (var i = 0; i < target.Stanzas.Count; i++)
            {
                var t = target.Stanzas[i];
                var at = -1;
                for (var j = 0; j < stanzas.Count; j++)
                    if (stanzas[j].Id == t.Id) { at = j; break; }

                if (at < 0)
                {
                    stanzas.Insert(Math.Min(i, stanzas.Count), new SongStanza(t.Id, t.Name, t.Lyrics) { Design = t.Design });
                    continue;
                }

                var s = stanzas[at];
                if (at != i) stanzas.Move(at, i);
                if (s.Name != t.Name) s.Name = t.Name;
                if (s.Lyrics != t.Lyrics) s.Lyrics = t.Lyrics;
                if (s.Design != t.Design) s.Design = t.Design;
            }
        }

        private sealed class Scope : IDisposable
        {
            private Action? _onDispose;
            public Scope(Action onDispose) => _onDispose = onDispose;

            public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }

    internal sealed class SongStateAction : IUndoableAction
    {
        // typing-like edits merge into the previous step when recorded soon after it
        private static readonly HashSet<string> Mergeable = new()
        {
            "Edit Title", "Edit Copyright", "Edit Lyrics", "Rename Section"
        };

        private readonly SongSnapshot _before;
        private SongSnapshot _after;
        private readonly Action<SongSnapshot> _write;

        public SongStateAction(SongSnapshot before, SongSnapshot after, string description, Action<SongSnapshot> write)
        {
            _before = before;
            _after = after;
            Description = description;
            _write = write;
        }

        public string Description { get; }

        public void Do() => _write(_after);
        public void Undo() => _write(_before);

        public bool TryMerge(IUndoableAction next)
        {
            if (next is not SongStateAction n || n.Description != Description || !Mergeable.Contains(Description))
                return false;

            _after = n._after;
            return true;
        }
    }

    /// <summary>The editable state of a song, with value equality.</summary>
    internal sealed class SongSnapshot : IEquatable<SongSnapshot>
    {
        public sealed record StanzaState(Guid Id, string Name, string Lyrics, Guid Design);

        private readonly string _key;

        private SongSnapshot(string title, string copyright, Guid design, string? motion, bool startOnTitle,
            bool endOnBlank, string? selectedArrangementId, IReadOnlyList<StanzaState> stanzas,
            IReadOnlyList<Guid> arrangement, IReadOnlyList<(string Name, Guid[] Ids)> arrangements)
        {
            Title = title;
            Copyright = copyright;
            Design = design;
            MotionBackgroundVideoPath = motion;
            StartOnTitleSlide = startOnTitle;
            EndOnBlankSlide = endOnBlank;
            SelectedArrangementId = selectedArrangementId;
            Stanzas = stanzas;
            Arrangement = arrangement;
            Arrangements = arrangements;

            var sb = new StringBuilder();
            void Add(string? s) => sb.Append(s == null ? "~" : s.Length + ":" + s).Append('\u0001');
            Add(title); Add(copyright); Add(design.ToString()); Add(motion);
            Add(startOnTitle.ToString()); Add(endOnBlank.ToString()); Add(selectedArrangementId);
            foreach (var s in stanzas) { Add(s.Id.ToString()); Add(s.Name); Add(s.Lyrics); Add(s.Design.ToString()); }
            Add("|");
            foreach (var id in arrangement) Add(id.ToString());
            Add("|");
            foreach (var (name, ids) in arrangements)
            {
                Add(name);
                foreach (var id in ids) Add(id.ToString());
                Add("/");
            }

            _key = sb.ToString();
        }

        public string Title { get; }
        public string Copyright { get; }
        public Guid Design { get; }
        public string? MotionBackgroundVideoPath { get; }
        public bool StartOnTitleSlide { get; }
        public bool EndOnBlankSlide { get; }
        public string? SelectedArrangementId { get; }
        public IReadOnlyList<StanzaState> Stanzas { get; }
        public IReadOnlyList<Guid> Arrangement { get; }
        public IReadOnlyList<(string Name, Guid[] Ids)> Arrangements { get; }

        public static SongSnapshot Of(SongItemInstance song) => new(
            song.Title,
            song.Copyright,
            song.Design,
            song.MotionBackgroundVideoPath,
            song.StartOnTitleSlide,
            song.EndOnBlankSlide,
            song.SelectedArrangementId,
            song.Stanzas.Select(s => new StanzaState(s.Id, s.Name ?? "", s.Lyrics ?? "", s.Design)).ToList(),
            song.Arrangement.ToList(),
            song.Arrangements.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value.ToArray())).ToList());

        public bool Equals(SongSnapshot? other) => other != null && _key == other._key;
        public override bool Equals(object? obj) => Equals(obj as SongSnapshot);
        public override int GetHashCode() => _key.GetHashCode();

        /// <summary>A menu label for the difference between two snapshots.</summary>
        public static string Describe(SongSnapshot a, SongSnapshot b)
        {
            var kinds = new List<string>();
            if (a.Title != b.Title) kinds.Add("Edit Title");
            if (a.Copyright != b.Copyright) kinds.Add("Edit Copyright");
            if (a.Design != b.Design) kinds.Add("Change Theme");
            if (a.MotionBackgroundVideoPath != b.MotionBackgroundVideoPath) kinds.Add("Change Background Video");
            if (a.StartOnTitleSlide != b.StartOnTitleSlide || a.EndOnBlankSlide != b.EndOnBlankSlide)
                kinds.Add("Change Song Options");

            var aIds = a.Stanzas.Select(s => s.Id).ToList();
            var bIds = b.Stanzas.Select(s => s.Id).ToList();
            string? stanzaKind = null;
            if (bIds.Count > aIds.Count) stanzaKind = "Add Section";
            else if (bIds.Count < aIds.Count) stanzaKind = "Delete Section";
            else if (!aIds.SequenceEqual(bIds)) stanzaKind = "Reorder Sections";
            else if (a.Stanzas.Zip(b.Stanzas).Any(p => p.First.Lyrics != p.Second.Lyrics)) stanzaKind = "Edit Lyrics";
            else if (a.Stanzas.Zip(b.Stanzas).Any(p => p.First.Name != p.Second.Name)) stanzaKind = "Rename Section";
            else if (a.Stanzas.Zip(b.Stanzas).Any(p => p.First.Design != p.Second.Design)) stanzaKind = "Change Theme";
            if (stanzaKind != null) kinds.Add(stanzaKind);

            // an arrangement change that comes with a section change (deleting a section drops it from the
            // arrangement) is part of that step
            var arrangementChanged = a.SelectedArrangementId != b.SelectedArrangementId
                                     || !a.Arrangement.SequenceEqual(b.Arrangement)
                                     || a.Arrangements.Count != b.Arrangements.Count
                                     || a.Arrangements.Zip(b.Arrangements).Any(p =>
                                         p.First.Name != p.Second.Name || !p.First.Ids.SequenceEqual(p.Second.Ids));
            if (arrangementChanged && stanzaKind == null) kinds.Add("Edit Arrangement");

            var distinct = kinds.Distinct().ToList();
            return distinct.Count == 1 ? distinct[0] : "Edit Song";
        }
    }
}
