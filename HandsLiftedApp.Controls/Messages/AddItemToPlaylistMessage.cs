using System.Collections.Generic;

namespace HandsLiftedApp.Models.PlaylistActions
{
    public class AddItemToPlaylistMessage
    {
        public List<string> filePaths { get; }

        public AddItemToPlaylistMessage(List<string> filePaths)
        {
            this.filePaths = filePaths;
        }
    }
}
