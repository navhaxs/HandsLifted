using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HandsLiftedApp.Comparer;

namespace HandsLiftedApp.Core.Models.Library
{
    // Picks a few representative files from inside a folder (including nested subfolders) to
    // preview it as a collage tile.
    public static class FolderPreviewFinder
    {
        public const int MaxPreviews = 4;
        private const int MaxFoldersVisited = 100;

        // Walks breadth-first (so shallow folders win) collecting each folder's matching files, then
        // spreads the previews across those folders: one each when there are enough folders, and
        // more per folder (evenly spaced through its files, for variety) when there are few - e.g. a
        // folder whose only content is a single subfolder of numbered clips still fills all four
        // cells. Bounded because the folder may be on a slow network/cloud-synced drive.
        public static IReadOnlyList<string> Find(string rootDirectory, ISet<string> extensions,
            CancellationToken cancellationToken = default)
        {
            var groups = new List<List<string>>();
            var comparer = new NaturalSortStringComparer(StringComparison.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(rootDirectory);
            var visited = 0;

            while (queue.Count > 0 && groups.Count < MaxPreviews && visited++ < MaxFoldersVisited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = queue.Dequeue();

                try
                {
                    var info = new DirectoryInfo(dir);

                    var files = info.GetFiles()
                        .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                        .Where(f => extensions.Contains(f.Extension.TrimStart('.')))
                        .OrderBy(f => f.Name, comparer)
                        .Select(f => f.FullName)
                        .ToList();
                    if (files.Count > 0) groups.Add(files);

                    foreach (var sub in info.GetDirectories()
                                 .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                                 .OrderBy(d => d.Name, comparer))
                    {
                        queue.Enqueue(sub.FullName);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Unreadable folder: skip it, keep whatever else we found.
                }
            }

            // Hand out the preview slots round-robin across folders, capped by each folder's size.
            var quota = new int[groups.Count];
            for (var remaining = MaxPreviews; remaining > 0;)
            {
                var progressed = false;
                for (var g = 0; g < groups.Count && remaining > 0; g++)
                {
                    if (quota[g] >= groups[g].Count) continue;
                    quota[g]++;
                    remaining--;
                    progressed = true;
                }
                if (!progressed) break;
            }

            var results = new List<string>();
            for (var g = 0; g < groups.Count; g++)
            {
                var n = groups[g].Count;
                for (var i = 0; i < quota[g]; i++)
                {
                    results.Add(groups[g][(int)Math.Floor((i + 0.5) * n / quota[g])]);
                }
            }

            return results;
        }
    }
}
