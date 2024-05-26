using Avalonia.Media.Imaging;

namespace HandsLiftedApp.Common
{
    public class BitmapCache
    {
        private int capacity;
        private Dictionary<string, Bitmap> cache;
        private LinkedList<string> lruList;

        public BitmapCache(int capacity)
        {
            this.capacity = capacity;
            cache = new Dictionary<string, Bitmap>();
            lruList = new LinkedList<string>();
        }

        public Bitmap? GetBitmap(string key)
        {
            if (cache.TryGetValue(key, out var bitmap))
            {
                // Move the key to the head of the list
                lruList.Remove(key);
                lruList.AddFirst(key);
                return bitmap;
            }

            return null;
        }

        public void AddBitmap(string key, Bitmap bitmap)
        {
            if (cache.ContainsKey(key))
            {
                // Move the key to the head of the list
                lruList.Remove(key);
                lruList.AddFirst(key);
            }
            else
            {
                if (cache.Count == capacity)
                {
                    // Remove the least recently used bitmap
                    var evictedKey = lruList.Last.Value;
                    lruList.RemoveLast();
                    cache.Remove(evictedKey);
                }

                lruList.AddFirst(key);
            }

            cache[key] = bitmap;
        }
    }
}