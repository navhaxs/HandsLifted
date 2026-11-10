using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Turns edits to a fixed list of playlist/item properties into undo steps.
    ///
    /// Most of these are written by two-way XAML bindings (a title box, a slider, an autoplay checkbox), so there
    /// is no call site to route through the undo stack. Instead the recorder listens for the property's change
    /// notification and records "old -> new" itself. It remembers each watched value so it knows the old one.
    ///
    /// Not recorded: changes made while an undo/redo is running (the stack ignores them), while the playlist is
    /// loading, on another thread than the one that created the playlist (background work such as an import
    /// finishing), or inside a <see cref="Suppress"/> scope. The remembered value is updated in every case, so the
    /// next real edit still has the right "old" value.
    ///
    /// Deliberately not watched: a song item's title (it is the shared library song's title - an editor-window
    /// concern, Phase 6) and scripture items (their reference is edited as one multi-field step, see
    /// <see cref="EditScriptureReferenceAction"/>).
    /// </summary>
    public sealed class PlaylistPropertyRecorder : IDisposable
    {
        private readonly PlaylistInstance _playlist;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly List<Attachment> _playlistAttachments = new();
        private readonly Dictionary<Item, List<Attachment>> _itemAttachments = new();
        private int _suppressed;

        public PlaylistPropertyRecorder(PlaylistInstance playlist)
        {
            _playlist = playlist;

            var spec = (string name, string label, Func<object?> read, Action<object?> write) =>
                new Watch(name, label, read, write, "playlist");

            Attach(playlist, _playlistAttachments, new[]
            {
                spec(nameof(PlaylistInstance.Title), "Rename Playlist",
                    () => playlist.Title, v => playlist.Title = (string)v!),
                spec(nameof(PlaylistInstance.LogoGraphicFile), "Change Logo",
                    () => playlist.LogoGraphicFile, v => playlist.LogoGraphicFile = (string?)v),
                spec(nameof(PlaylistInstance.SlideTransitionDurationMs), "Change Fade Duration",
                    () => playlist.SlideTransitionDurationMs, v => playlist.SlideTransitionDurationMs = (double)v!),
                spec(nameof(PlaylistInstance.DefaultSongThemeId), "Change Song Theme",
                    () => playlist.DefaultSongThemeId, v => playlist.DefaultSongThemeId = (Guid?)v),
                spec(nameof(PlaylistInstance.DefaultSongMotionThemeId), "Change Song Theme",
                    () => playlist.DefaultSongMotionThemeId, v => playlist.DefaultSongMotionThemeId = (Guid?)v),
                spec(nameof(PlaylistInstance.DefaultScriptureThemeId), "Change Scripture Theme",
                    () => playlist.DefaultScriptureThemeId, v => playlist.DefaultScriptureThemeId = (Guid?)v),
            });
        }

        /// <summary>Edits made inside the returned scope are not recorded (programmatic changes, not user edits).</summary>
        public IDisposable Suppress()
        {
            Interlocked.Increment(ref _suppressed);
            return new SuppressScope(this);
        }

        /// <summary>Starts watching a playlist item.</summary>
        public void AttachItem(Item item)
        {
            if (_itemAttachments.ContainsKey(item)) return;

            var uuid = item.UUID;
            Item Find() => _playlist.Items.FirstOrDefault(i => i.UUID == uuid)
                           ?? throw new InvalidOperationException($"Item {uuid} is no longer in the playlist");

            var attachments = new List<Attachment>();
            _itemAttachments[item] = attachments;

            var itemWatches = new List<Watch>
            {
                new(nameof(Item.SlideTransitionDurationMs), "Change Fade Duration",
                    () => item.SlideTransitionDurationMs, v => Find().SlideTransitionDurationMs = (double?)v, uuid),
            };

            if (item is not SongItemInstance && item is not ScriptureItemInstance)
            {
                itemWatches.Add(new Watch(nameof(Item.Title), "Rename",
                    () => item.Title, v => Find().Title = (string)v!, uuid));
            }

            // A scripture item's own theme (Item > Edit > Theme picker; Guid.Empty = ride the playlist/app default).
            // A song's theme is NOT watched: it lives on the shared library song and is announced asynchronously
            // through the library (a library edit, Phase 6/7).
            if (item is ScriptureItemInstance)
            {
                itemWatches.Add(new Watch(nameof(ScriptureItemInstance.Design), "Change Theme",
                    () => ((ScriptureItemInstance)item).Design,
                    v =>
                    {
                        var s = (ScriptureItemInstance)Find();
                        s.Design = (Guid)v!;
                        // same follow-up as ScriptureItemInstance.ResolvedDesignTheme's setter
                        _ = s.GenerateSlidesAsync().ContinueWith(
                            t => Serilog.Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", s.Title),
                            TaskContinuationOptions.OnlyOnFaulted);
                    }, uuid));
            }

            if (item is SongItemInstance song)
            {
                itemWatches.Add(new Watch(nameof(SongItemInstance.MotionBackgroundVideoOverride),
                    "Change Background Video", () => song.MotionBackgroundVideoOverride,
                    v => ((SongItemInstance)Find()).MotionBackgroundVideoOverride = (string?)v, uuid));
            }

            Attach(item, attachments, itemWatches, onSourceChanged: name =>
            {
                // the timer object itself was swapped: follow it
                if (name == nameof(MediaGroupItem.AutoAdvanceTimer) && item is MediaGroupItem g)
                    AttachTimer(g, uuid, Find, attachments);
            });

            if (item is MediaGroupItem group) AttachTimer(group, uuid, Find, attachments);
        }

        public void DetachItem(Item item)
        {
            if (!_itemAttachments.Remove(item, out var attachments)) return;
            foreach (var a in attachments) a.Dispose();
        }

        /// <summary>Drops every item watch and re-attaches the given items (the item collection was replaced).</summary>
        public void ResyncItems(IEnumerable<Item> items)
        {
            foreach (var item in _itemAttachments.Keys.ToList()) DetachItem(item);
            foreach (var item in items) AttachItem(item);
        }

        public void Dispose()
        {
            foreach (var item in _itemAttachments.Keys.ToList()) DetachItem(item);
            foreach (var a in _playlistAttachments) a.Dispose();
            _playlistAttachments.Clear();
        }

        private void AttachTimer(MediaGroupItem group, Guid uuid, Func<Item> find, List<Attachment> attachments)
        {
            // drop a previous timer's watch (the timer was replaced)
            foreach (var old in attachments.Where(a => a.Tag == "timer").ToList())
            {
                old.Dispose();
                attachments.Remove(old);
            }

            var timer = group.AutoAdvanceTimer;
            Attach(timer, attachments, new[]
            {
                new Watch(nameof(ItemAutoAdvanceTimer.IsEnabled), "Change Autoplay Timer",
                    () => timer.IsEnabled,
                    v => ((MediaGroupItem)find()).AutoAdvanceTimer.IsEnabled = (bool)v!, uuid, "timer"),
                new Watch(nameof(ItemAutoAdvanceTimer.IntervalMs), "Change Autoplay Interval",
                    () => timer.IntervalMs,
                    v => ((MediaGroupItem)find()).AutoAdvanceTimer.IntervalMs = (int)v!, uuid, "timer"),
            }, tag: "timer");
        }

        private void Attach(INotifyPropertyChanged source, List<Attachment> into, IEnumerable<Watch> watches,
            Action<string?>? onSourceChanged = null, string? tag = null)
        {
            var attachment = new Attachment(source, watches.ToList(), tag);
            attachment.Handler = (_, e) =>
            {
                onSourceChanged?.Invoke(e.PropertyName);
                OnPropertyChanged(attachment, e.PropertyName);
            };
            source.PropertyChanged += attachment.Handler;
            into.Add(attachment);
        }

        private void OnPropertyChanged(Attachment attachment, string? propertyName)
        {
            foreach (var watch in attachment.Watches)
            {
                if (propertyName != null && propertyName != watch.Name) continue;

                var now = watch.Read();
                var old = watch.Last;
                watch.Last = now;
                if (Equals(old, now)) continue;

                if (!ShouldRecord()) continue;

                _playlist.UndoStack.Record(new PropertyEditAction(
                    (watch.Target, watch.Scope, watch.Name), watch.Name, watch.Label, old, now, watch.Write));
            }
        }

        private bool ShouldRecord() =>
            Volatile.Read(ref _suppressed) == 0
            && !_playlist.IsPlaylistLoading
            && !_playlist.UndoStack.IsApplying
            && Environment.CurrentManagedThreadId == _ownerThread;

        private sealed class Watch
        {
            public Watch(string name, string label, Func<object?> read, Action<object?> write, object target,
                string scope = "item")
            {
                Name = name;
                Label = label;
                Read = read;
                Write = write;
                Target = target;
                Scope = scope;
                Last = read();
            }

            public string Name { get; }
            public string Label { get; }
            public Func<object?> Read { get; }
            public Action<object?> Write { get; }
            public object Target { get; }
            public string Scope { get; }
            public object? Last { get; set; }
        }

        private sealed class Attachment : IDisposable
        {
            public Attachment(INotifyPropertyChanged source, List<Watch> watches, string? tag)
            {
                Source = source;
                Watches = watches;
                Tag = tag;
            }

            public INotifyPropertyChanged Source { get; }
            public List<Watch> Watches { get; }
            public string? Tag { get; }
            public PropertyChangedEventHandler? Handler { get; set; }

            public void Dispose()
            {
                if (Handler != null) Source.PropertyChanged -= Handler;
                Handler = null;
            }
        }

        private sealed class SuppressScope : IDisposable
        {
            private PlaylistPropertyRecorder? _owner;
            public SuppressScope(PlaylistPropertyRecorder owner) => _owner = owner;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null) Interlocked.Decrement(ref owner._suppressed);
            }
        }
    }
}
