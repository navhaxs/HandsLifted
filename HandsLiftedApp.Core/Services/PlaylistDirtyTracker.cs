using System;
using System.Security.Cryptography;
using HandsLiftedApp.Core.Models;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    /// <summary>
    /// Single owner of <see cref="PlaylistInstance.IsDirty"/>'s truth. "Dirty" means the playlist's
    /// serialized form differs from the form last saved to (or loaded from) its file.
    ///
    /// Change events (<c>Playlist.Changed</c>, collection changes, <c>ItemDataModified</c>) are only
    /// <em>hints</em>: they optimistically set <c>IsDirty = true</c> so the indicator reacts at once, and
    /// <see cref="Refresh"/> then decides the real answer. That means a missed change event can't lose
    /// work (the next Refresh sees the difference), and undoing back to the saved state clears the flag.
    ///
    /// Independent of auto-save: it must work with auto-save switched off.
    /// UI thread only (serialization walks runtime objects).
    /// </summary>
    public sealed class PlaylistDirtyTracker
    {
        private readonly PlaylistInstance _playlist;

        // Hash of the last saved/loaded serialized state. Null = no file baseline (untitled playlist):
        // then dirty simply means "not empty".
        private byte[]? _baseline;

        // The file is known to need rewriting regardless of content (e.g. a load-time migration).
        private bool _forceDirty;

        public PlaylistDirtyTracker(PlaylistInstance playlist)
        {
            _playlist = playlist;
        }

        public static byte[] ComputeHash(PlaylistInstance playlist, string filePath) =>
            SHA256.HashData(HandsLiftedDocXmlSerializer.SerializePlaylistToBytes(playlist, filePath));

        /// <summary>The current state is what is on disk (manual save, auto-save).</summary>
        public void MarkSaved(byte[] savedStateHash)
        {
            _baseline = savedStateHash;
            _forceDirty = false;
            _playlist.IsDirty = false;
        }

        /// <summary>A brand-new empty untitled playlist: clean until something is added.</summary>
        public void MarkPristine()
        {
            _baseline = null;
            _forceDirty = false;
            _playlist.IsDirty = false;
        }

        /// <summary>
        /// A playlist file has just finished loading. Clean, unless <paramref name="needsResave"/> says
        /// the file must be rewritten (migration), in which case it stays dirty until saved.
        /// </summary>
        public void MarkLoaded(bool needsResave)
        {
            _baseline = null;
            _forceDirty = needsResave;

            var path = _playlist.PlaylistFilePath;
            if (!needsResave && !string.IsNullOrEmpty(path))
            {
                try
                {
                    _baseline = ComputeHash(_playlist, path);
                    _playlist.IsDirty = false;
                    return;
                }
                catch (Exception ex)
                {
                    // Fall through to the no-baseline rule: a non-empty playlist reads as dirty.
                    Log.Warning(ex, "[DOC] Could not baseline loaded playlist for dirty tracking");
                }
            }

            _playlist.IsDirty = needsResave || !IsEmpty;
        }

        /// <summary>
        /// Recomputes <see cref="PlaylistInstance.IsDirty"/> from the actual state and returns it.
        /// Call before any decision that depends on it (exit / new / load prompts).
        /// </summary>
        public bool Refresh()
        {
            // Mid-load the playlist is half built; MarkLoaded settles it afterwards.
            if (_playlist.IsPlaylistLoading) return _playlist.IsDirty;

            bool dirty;
            var path = _playlist.PlaylistFilePath;
            if (_forceDirty)
            {
                dirty = true;
            }
            else if (_baseline == null || string.IsNullOrEmpty(path))
            {
                dirty = !IsEmpty;
            }
            else
            {
                try
                {
                    dirty = !ComputeHash(_playlist, path).AsSpan().SequenceEqual(_baseline);
                }
                catch (Exception ex)
                {
                    // Can't prove it's clean, so don't say it is (safe side for the unsaved-changes prompts).
                    Log.Warning(ex, "[DOC] Dirty check failed to serialize playlist; treating as dirty");
                    dirty = true;
                }
            }

            _playlist.IsDirty = dirty;
            return dirty;
        }

        private bool IsEmpty => string.IsNullOrEmpty(_playlist.Title) && _playlist.Items.Count == 0;
    }
}
