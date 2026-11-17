using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    /// <summary>
    /// Works out which slide of a re-synced item is "the same slide" as before, so the selection can follow it.
    /// A slide's key is its source id when the source has one (PowerPoint slide id, Google Slides object id), else
    /// a hash of its exported image (PDF, legacy .ppt). Keys that can't be worked out are null and never match.
    /// </summary>
    internal static class SlideIdentity
    {
        private static readonly HashSet<string> ImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// One key per slide file: the source ids when there is exactly one per file ("id:" prefix), otherwise
        /// image hashes ("sha:" prefix; null for files that aren't images, such as videos).
        /// </summary>
        public static List<string?> KeysFor(IReadOnlyList<string> slideFiles, IReadOnlyList<string>? sourceIds)
        {
            if (sourceIds != null && sourceIds.Count == slideFiles.Count && sourceIds.All(id => !string.IsNullOrEmpty(id)))
                return sourceIds.Select(id => (string?)("id:" + id)).ToList();

            return slideFiles.Select(HashKey).ToList();
        }

        private static string? HashKey(string path)
        {
            try
            {
                if (!ImageExtensions.Contains(Path.GetExtension(path))) return null;
                using var stream = File.OpenRead(path);
                return "sha:" + Convert.ToHexString(SHA1.HashData(stream));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the slide at <paramref name="oldIndex"/> of the old set is in the new set, or -1 if it can't be told.
        /// If several new slides share the key (e.g. identical blank pages) the one closest to the old position wins.
        /// </summary>
        public static int MapIndex(IReadOnlyList<string?>? oldKeys, int oldIndex, IReadOnlyList<string?> newKeys)
        {
            if (oldKeys == null || oldIndex < 0 || oldIndex >= oldKeys.Count) return -1;
            var key = oldKeys[oldIndex];
            if (key == null) return -1;

            var best = -1;
            for (var i = 0; i < newKeys.Count; i++)
            {
                if (newKeys[i] != key) continue;
                if (best == -1 || Math.Abs(i - oldIndex) < Math.Abs(best - oldIndex)) best = i;
            }

            return best;
        }
    }
}
