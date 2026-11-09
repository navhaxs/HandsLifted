using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HandsLiftedApp.Core.Models;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public static class PlaylistDocumentService
    {
        public static void SaveDocument(PlaylistInstance playlist)
        {
            if (playlist.PlaylistFilePath == null)
            {
                throw new FileNotFoundException("Playlist cannot be saved. PlaylistFilePath is null.");
            }

            Log.Information("Saving playlist to {FilePath}", playlist.PlaylistFilePath);
            // Serialize once and hash those exact bytes so the dirty baseline is precisely what hit disk.
            byte[] bytes = HandsLiftedDocXmlSerializer.SerializePlaylistToBytes(playlist, playlist.PlaylistFilePath);
            File.WriteAllBytes(playlist.PlaylistFilePath, bytes);
            playlist.DirtyTracker.MarkSaved(SHA256.HashData(bytes));

            // disk now holds something we didn't hash; let the next autosave re-learn it
            LastSavedHashes.Remove(playlist.PlaylistFilePath);
        }

        public enum AutoSaveResult
        {
            /// <summary>Nothing to do: no file path yet, or the playlist is mid-load.</summary>
            Skipped,
            /// <summary>Serialized state already matches the file on disk; nothing was written.</summary>
            Unchanged,
            /// <summary>The playlist file was overwritten.</summary>
            Saved
        }

        // Paths already backed up since they were loaded/created this session.
        private static readonly HashSet<string> BackedUpPaths = new(StringComparer.OrdinalIgnoreCase);

        // SHA-256 of the bytes we last wrote to (or confirmed on) disk for each path, so idle ticks
        // compare against memory instead of re-reading the file. Absent = disk state unknown.
        private static readonly Dictionary<string, byte[]> LastSavedHashes = new(StringComparer.OrdinalIgnoreCase);

        public static string GetBackupFilePath(string fullPath) => fullPath + ".bak";

        /// <summary>
        /// Call when a playlist file is (re)loaded: the next autosave re-snapshots the loaded state
        /// into the <c>.bak</c> file and re-checks the file on disk once.
        /// </summary>
        public static void ResetAutoSaveState(string? playlistFilePath)
        {
            if (string.IsNullOrEmpty(playlistFilePath)) return;
            BackedUpPaths.Remove(playlistFilePath);
            LastSavedHashes.Remove(playlistFilePath);
        }

        /// <summary>
        /// Saves the playlist over its real file. Does not rely on <see cref="PlaylistInstance.IsDirty"/>:
        /// it serializes and compares a hash against the last-saved hash held in memory, so a missed
        /// change event can't lose data, and an idle tick neither writes nor reads the file. Disk is
        /// read once per load (no hash known yet). The first write after load first copies the
        /// existing file to <c>.bak</c>. Must be called on the UI thread (walks runtime objects).
        /// </summary>
        public static AutoSaveResult AutoSaveToFile(PlaylistInstance playlist)
        {
            string? path = playlist.PlaylistFilePath;
            if (string.IsNullOrEmpty(path) || playlist.IsPlaylistLoading) return AutoSaveResult.Skipped;

            byte[] bytes = HandsLiftedDocXmlSerializer.SerializePlaylistToBytes(playlist, path);
            byte[] hash = SHA256.HashData(bytes);

            bool exists = File.Exists(path);
            if (LastSavedHashes.TryGetValue(path, out var lastHash))
            {
                if (hash.AsSpan().SequenceEqual(lastHash))
                {
                    playlist.DirtyTracker.MarkSaved(hash);
                    return AutoSaveResult.Unchanged;
                }
            }
            else if (exists)
            {
                // First check since load / manual save: learn what's on disk once.
                byte[] onDisk = File.ReadAllBytes(path);
                if (SHA256.HashData(onDisk).AsSpan().SequenceEqual(hash))
                {
                    LastSavedHashes[path] = hash;
                    playlist.DirtyTracker.MarkSaved(hash);
                    return AutoSaveResult.Unchanged;
                }
            }

            if (exists && BackedUpPaths.Add(path))
            {
                File.Copy(path, GetBackupFilePath(path), overwrite: true);
            }

            // Write beside the target then swap in, so a crash mid-write can't truncate the playlist.
            string tempPath = path + ".autosave.tmp";
            File.WriteAllBytes(tempPath, bytes);
            if (exists) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
            LastSavedHashes[path] = hash;

            Log.Information("Autosaved playlist to {FilePath}", path);
            playlist.DirtyTracker.MarkSaved(hash);
            playlist.LastSaved = DateTime.Now;
            return AutoSaveResult.Saved;
        }

        /// <summary>
        /// Path of the <c>name.autosave.ext</c> recovery sidecar that earlier versions wrote beside
        /// the playlist. No longer written or read; only cleaned up.
        /// </summary>
        public static string GetLegacyAutoSaveSidecarPath(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
            var extension = Path.GetExtension(fullPath);
            return Path.Combine(directory ?? string.Empty, filenameWithoutExtension + ".autosave" + extension);
        }

        /// <summary>
        /// Deletes a leftover recovery sidecar from an earlier version. Safe to remove this (and its
        /// call on load) once old sidecars are unlikely to exist.
        /// </summary>
        public static void DeleteLegacyAutoSaveSidecar(string? playlistFilePath)
        {
            if (string.IsNullOrEmpty(playlistFilePath)) return;

            string sidecarPath = GetLegacyAutoSaveSidecarPath(playlistFilePath);
            if (File.Exists(sidecarPath))
            {
                Log.Information("Deleting legacy autosave sidecar {SidecarPath}", sidecarPath);
                File.Delete(sidecarPath);
            }
        }
    }
}