using System.Threading;
using Avalonia.Media.Imaging;

namespace HandsLiftedApp.Common
{
    // Represents one tracked reference to a Bitmap handed out by BitmapCache.Acquire/AddAndAcquire.
    // MUST be released (Release()) once the holder no longer needs the bitmap - e.g. when the
    // slide instance that acquired it is torn down. Release() is idempotent.
    public sealed class BitmapCacheLease
    {
        public Bitmap Bitmap { get; }
        private readonly Action _release;
        private int _released;

        internal BitmapCacheLease(Bitmap bitmap, Action release)
        {
            Bitmap = bitmap;
            _release = release;
        }

        public void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _release();
            }
        }
    }

    // LRU cache of path-keyed Bitmaps. Two APIs are offered:
    //
    //  - GetBitmap/AddBitmap: the original, untracked lookup - used by callers with no clean
    //    lifecycle hook to release a reference (XAML binding converters, one-off preview
    //    bindings). Entries touched only through this path are never disposed by this cache -
    //    same leak-safe-but-leaky behaviour as before this class had any refcounting, since we
    //    have no way to know whether such a caller is still displaying the bitmap.
    //  - Acquire/AddAndAcquire (+ the returned BitmapCacheLease.Release()): for long-lived,
    //    explicitly-torn-down consumers (currently: ImageSlideInstance) that need eviction to
    //    actually free native memory instead of leaking it. An entry is only disposed once it has
    //    been evicted/replaced AND every tracked lease on it has been released - so a bitmap still
    //    referenced by a live ImageSlideInstance is never disposed out from under it, even if it
    //    falls out of the LRU window.
    //
    // Residual risk: if the exact same file path is ALSO handed out via the untracked path (e.g.
    // shown as a logo preview) while an ImageSlideInstance's tracked lease on it is released and
    // the entry has been evicted, this cache has no way to know the untracked caller still needs
    // it. Closing that fully would require every consumer to participate in tracking, which the
    // XAML-binding-driven callers can't easily do. Given how rarely the same file is used as both
    // a slide image and a logo/preview at once, this is accepted as a narrow, low-impact gap.
    public class BitmapCache
    {
        private sealed class Entry
        {
            public Bitmap Bitmap = null!;
            public int RefCount;
            public bool InCache;
            public bool EverTracked;
            public bool Disposed;
        }

        private readonly object _lock = new();
        private readonly int capacity;
        private readonly Dictionary<string, Entry> cache;
        private readonly LinkedList<string> lruList;

        public BitmapCache(int capacity)
        {
            this.capacity = capacity;
            cache = new Dictionary<string, Entry>();
            lruList = new LinkedList<string>();
        }

        public Bitmap? GetBitmap(string key)
        {
            lock (_lock)
            {
                if (cache.TryGetValue(key, out var entry))
                {
                    Touch(key);
                    return entry.Bitmap;
                }

                return null;
            }
        }

        public void AddBitmap(string key, Bitmap bitmap)
        {
            lock (_lock)
            {
                GetOrReplaceEntryNoLock(key, bitmap);
            }
        }

        // Returns a tracked lease on the cached bitmap for `key`, if present. The caller MUST
        // eventually call Release() on the returned lease.
        public BitmapCacheLease? Acquire(string key)
        {
            lock (_lock)
            {
                if (!cache.TryGetValue(key, out var entry))
                {
                    return null;
                }

                Touch(key);
                return AcquireLeaseNoLock(entry);
            }
        }

        // Registers a freshly decoded bitmap for `key` and returns a tracked lease on it, as if
        // via Acquire.
        public BitmapCacheLease AddAndAcquire(string key, Bitmap bitmap)
        {
            lock (_lock)
            {
                var entry = GetOrReplaceEntryNoLock(key, bitmap);
                return AcquireLeaseNoLock(entry);
            }
        }

        private BitmapCacheLease AcquireLeaseNoLock(Entry entry)
        {
            entry.RefCount++;
            entry.EverTracked = true;
            return new BitmapCacheLease(entry.Bitmap, () => ReleaseEntry(entry));
        }

        private void Touch(string key)
        {
            lruList.Remove(key);
            lruList.AddFirst(key);
        }

        // Must be called with _lock held.
        private Entry GetOrReplaceEntryNoLock(string key, Bitmap bitmap)
        {
            if (cache.TryGetValue(key, out var existing))
            {
                lruList.Remove(key);
                EvictNoLock(existing);
            }
            else if (cache.Count == capacity)
            {
                var evictedKey = lruList.Last!.Value;
                lruList.RemoveLast();
                if (cache.Remove(evictedKey, out var evicted))
                {
                    EvictNoLock(evicted);
                }
            }

            var entry = new Entry { Bitmap = bitmap, InCache = true };
            cache[key] = entry;
            lruList.AddFirst(key);
            return entry;
        }

        // Marks an entry as no longer the live cache occupant. Must be called with _lock held.
        private void EvictNoLock(Entry entry)
        {
            entry.InCache = false;
            DisposeIfUnreferencedNoLock(entry);
        }

        private void ReleaseEntry(Entry entry)
        {
            lock (_lock)
            {
                entry.RefCount--;
                DisposeIfUnreferencedNoLock(entry);
            }
        }

        private static void DisposeIfUnreferencedNoLock(Entry entry)
        {
            if (entry.Disposed || entry.InCache || entry.RefCount > 0 || !entry.EverTracked)
            {
                return;
            }

            entry.Disposed = true;
            entry.Bitmap.Dispose();
        }
    }
}
