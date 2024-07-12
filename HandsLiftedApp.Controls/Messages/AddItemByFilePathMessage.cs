using System.Collections.Generic;

namespace HandsLiftedApp.Models.PlaylistActions
{
    public class AddItemByFilePathMessage
    {
        public List<string> filePaths { get; }
        
        public int? insertIndex { get; }

        public AddItemByFilePathMessage(List<string> filePaths, int? insertIndex = null)
        {
            this.filePaths = filePaths;
            this.insertIndex = insertIndex;
        }
    }
}
