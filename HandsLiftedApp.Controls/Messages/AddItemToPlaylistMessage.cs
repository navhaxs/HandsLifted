using System.Collections.Generic;

namespace HandsLiftedApp.Models.PlaylistActions
{
    public class AddItemToPlaylistMessage
    {
        public List<string> filePaths { get; }
        
        public int? insertIndex { get; }

        public AddItemToPlaylistMessage(List<string> filePaths, int? insertIndex = null)
        {
            this.filePaths = filePaths;
            this.insertIndex = insertIndex;
        }
    }
}
