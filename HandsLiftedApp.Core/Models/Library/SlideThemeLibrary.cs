using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using Avalonia.Threading;
using DebounceThrottle;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using Serilog;
using System.Collections.ObjectModel;

namespace HandsLiftedApp.Core.Models.Library
{
    /// <summary>
    /// App-wide set of slide themes, one XML file per theme in a single folder. Themes is the
    /// single source of truth: adding to it persists the theme, editing a theme debounces a
    /// save, removing from it deletes the file. Slot 0 holds the built-in default theme (not
    /// file-backed) when one is supplied to Initialize.
    /// All members are UI-thread only.
    /// </summary>
    public sealed class SlideThemeLibrary : IDisposable
    {
        private sealed class Entry
        {
            public string FilePath = "";
            public string LastName = "";
            public byte[] LastBytes = Array.Empty<byte>();
            public IDisposable? ChangedSubscription;
            public readonly DebounceDispatcher Debouncer = new(500);
        }

        private readonly Dictionary<Guid, Entry> _entries = new();
        private readonly Func<string?> _mediaLibraryPath;
        private readonly Action<Action> _postToSaveThread;
        private readonly Subject<(Guid Id, Exception Error)> _saveFailed = new();
        private Guid? _builtInId;
        private bool _syncing;

        public ObservableCollection<BaseSlideTheme> Themes { get; } = new();
        public string? Folder { get; private set; }
        public IObservable<(Guid Id, Exception Error)> SaveFailed => _saveFailed;

        public SlideThemeLibrary(Func<string?>? mediaLibraryPath = null, Action<Action>? postToSaveThread = null)
        {
            _mediaLibraryPath = mediaLibraryPath ?? (() => Globals.Instance.AppPreferences?.MediaLibraryPath);
            _postToSaveThread = postToSaveThread ?? (a => Dispatcher.UIThread.Post(a));
            Themes.CollectionChanged += OnThemesChanged;
        }

        public bool Contains(Guid id) => Themes.Any(t => t.Id == id);

        public void Initialize(string folder, BaseSlideTheme? builtInDefault)
        {
            // Write out any edit still inside its debounce window to the OLD folder first.
            FlushPendingSaves();

            _syncing = true;
            try
            {
                DetachAll();
                Themes.Clear();
                _builtInId = builtInDefault?.Id;
                if (builtInDefault != null) Themes.Add(builtInDefault);

                try
                {
                    Directory.CreateDirectory(folder);
                    Folder = folder;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "SlideThemeLibrary: cannot use themes folder {Folder} - themes will not be saved", folder);
                    Folder = null;
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*.xml", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    LoadFile(file);
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void LoadFile(string file)
        {
            BaseSlideTheme? theme;
            try
            {
                using var stream = File.OpenRead(file);
                if (!SlideThemeXmlSerializer.TryDeserialize(stream, out theme) || theme == null)
                {
                    Log.Warning("SlideThemeLibrary: skipping unreadable theme file {File}", file);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SlideThemeLibrary: skipping unreadable theme file {File}", file);
                return;
            }

            if (Contains(theme.Id))
            {
                Log.Warning("SlideThemeLibrary: ignoring {File} - theme Id {Id} already loaded", file, theme.Id);
                return;
            }

            var media = _mediaLibraryPath();
            theme.BackgroundGraphicFilePath = ToAbsolute(theme.BackgroundGraphicFilePath, media);
            theme.DefaultMotionBackgroundVideoPath = ToAbsolute(theme.DefaultMotionBackgroundVideoPath, media);

            Themes.Add(theme);
            Attach(theme, file);
        }

        private void Attach(BaseSlideTheme theme, string filePath)
        {
            var entry = new Entry { FilePath = filePath };
            if (filePath != "")
            {
                entry.LastName = theme.Name;
                entry.LastBytes = RenderBytes(theme) ?? Array.Empty<byte>();
            }

            var id = theme.Id;
            entry.ChangedSubscription = theme.Changed.Subscribe(_ => ScheduleSave(id));
            _entries[id] = entry;
        }

        private void OnThemesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncing) return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                Log.Warning("SlideThemeLibrary: Themes was reset outside Initialize - files on disk are unchanged");
                return;
            }

            if (e.OldItems != null)
            {
                foreach (BaseSlideTheme theme in e.OldItems) Detach(theme, deleteFile: true);
            }

            if (e.NewItems != null)
            {
                foreach (BaseSlideTheme theme in e.NewItems)
                {
                    if (theme.Id == _builtInId || Folder == null) continue;
                    if (_entries.ContainsKey(theme.Id))
                    {
                        Log.Warning("SlideThemeLibrary: theme Id {Id} added twice - second copy is not persisted", theme.Id);
                        continue;
                    }
                    Attach(theme, "");
                    ScheduleSave(theme.Id);
                }
            }
        }

        private void Detach(BaseSlideTheme theme, bool deleteFile)
        {
            if (!_entries.TryGetValue(theme.Id, out var entry)) return;
            entry.ChangedSubscription?.Dispose();
            _entries.Remove(theme.Id);
            if (!deleteFile || entry.FilePath == "") return;
            try
            {
                if (File.Exists(entry.FilePath)) File.Delete(entry.FilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SlideThemeLibrary: failed to delete {File}", entry.FilePath);
            }
        }

        private void DetachAll()
        {
            foreach (var entry in _entries.Values) entry.ChangedSubscription?.Dispose();
            _entries.Clear();
        }

        private void ScheduleSave(Guid id)
        {
            if (_syncing || !_entries.TryGetValue(id, out var entry)) return;
            entry.Debouncer.Debounce(() => _postToSaveThread(() => SaveNow(id)));
        }

        public void SaveNow(Guid id)
        {
            if (Folder == null || !_entries.TryGetValue(id, out var entry)) return;
            var theme = Themes.FirstOrDefault(t => t.Id == id);
            if (theme == null) return;

            try
            {
                var bytes = RenderBytes(theme) ?? throw new InvalidOperationException("Theme could not be serialized");
                var nameChanged = theme.Name != entry.LastName;
                if (!nameChanged && bytes.AsSpan().SequenceEqual(entry.LastBytes) && File.Exists(entry.FilePath))
                    return;

                var path = nameChanged || entry.FilePath == "" ? UniquePath(theme.Name, id) : entry.FilePath;
                File.WriteAllBytes(path, bytes);
                if (entry.FilePath != "" && !string.Equals(path, entry.FilePath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(entry.FilePath))
                {
                    File.Delete(entry.FilePath);
                }

                entry.FilePath = path;
                entry.LastName = theme.Name;
                entry.LastBytes = bytes;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SlideThemeLibrary: failed to save theme {Id}", id);
                _saveFailed.OnNext((id, ex));
            }
        }

        public void FlushPendingSaves()
        {
            foreach (var id in _entries.Keys.ToList()) SaveNow(id);
        }

        private string UniquePath(string name, Guid id)
        {
            var baseName = FilenameUtils.ReplaceInvalidChars(string.IsNullOrWhiteSpace(name) ? id.ToString() : name);
            for (var n = 1; ; n++)
            {
                var fileName = n == 1 ? $"{baseName}.xml" : $"{baseName} ({n}).xml";
                var candidate = Path.Combine(Folder!, fileName);
                var ownedByOther = _entries.Any(kv => kv.Key != id
                    && string.Equals(kv.Value.FilePath, candidate, StringComparison.OrdinalIgnoreCase));
                var ownedByThis = _entries.TryGetValue(id, out var own)
                    && string.Equals(own.FilePath, candidate, StringComparison.OrdinalIgnoreCase);
                if (!ownedByOther && (ownedByThis || !File.Exists(candidate))) return candidate;
            }
        }

        // Serializes a COPY with asset paths relativized, so the live theme keeps absolute paths
        // and mutating it never raises Changed (which would re-trigger a save).
        private byte[]? RenderBytes(BaseSlideTheme theme)
        {
            var copy = new BaseSlideTheme();
            copy.CopyFrom(theme);
            var media = _mediaLibraryPath();
            copy.BackgroundGraphicFilePath = ToStored(copy.BackgroundGraphicFilePath, media);
            copy.DefaultMotionBackgroundVideoPath = ToStored(copy.DefaultMotionBackgroundVideoPath, media);
            return SlideThemeXmlSerializer.TrySerializeToBytes(copy, out var bytes) ? bytes : null;
        }

        private static bool IsAvares(string path) => path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase);

        private static string? ToAbsolute(string? path, string? media)
        {
            if (string.IsNullOrWhiteSpace(path) || IsAvares(path) || Path.IsPathFullyQualified(path)) return path;
            return string.IsNullOrWhiteSpace(media) ? path : RelativeFilePathResolver.ToAbsolutePath(media, path);
        }

        private static string? ToStored(string? path, string? media)
        {
            if (string.IsNullOrWhiteSpace(path) || IsAvares(path) || !Path.IsPathFullyQualified(path)) return path;
            if (string.IsNullOrWhiteSpace(media) || !RelativeFilePathResolver.IsUnderDirectory(media, path)) return path;
            return RelativeFilePathResolver.ToRelativePath(media, path);
        }

        public void Dispose() => DetachAll();
    }
}
