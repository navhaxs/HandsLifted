using System.Collections.Generic;

namespace HandsLiftedApp.Models.PlaylistActions
{
    internal class AddItemToPlaylistMessage
    {
        public List<string> filenames { get; }

        public AddItemToPlaylistMessage(List<string> filenames)
        {
            this.filenames = filenames;
        }
    }
}
