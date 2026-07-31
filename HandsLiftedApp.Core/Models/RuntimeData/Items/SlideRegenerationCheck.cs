using System.IO;
using System.Linq;
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
    }
}
