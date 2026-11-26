using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class SlideRegenerationCheck
    {
        public static bool NeedsSlideRegeneration(MediaGroupItem group)
        {
            if (group.Items.Count == 0)
            {
                return true;
            }

            return group.Items
                .OfType<MediaGroupItem.MediaItem>()
                .Any(mediaItem => mediaItem.SourceMediaFilePath == null || !File.Exists(mediaItem.SourceMediaFilePath));
        }

        /// <summary>
        /// Same answer as <see cref="NeedsSlideRegeneration"/>, but the <c>File.Exists</c> probes (which can
        /// be slow on a network drive) run on the thread pool. The group's items are read on the calling
        /// thread (it is a UI-bound collection); only the file-system checks move.
        /// </summary>
        public static Task<bool> NeedsSlideRegenerationAsync(MediaGroupItem group)
        {
            if (group.Items.Count == 0)
            {
                return Task.FromResult(true);
            }

            var paths = group.Items
                .OfType<MediaGroupItem.MediaItem>()
                .Select(mediaItem => mediaItem.SourceMediaFilePath)
                .ToList();
            return Task.Run(() => paths.Any(path => path == null || !File.Exists(path)));
        }
    }
}
