namespace HandsLiftedApp.Core.Models.UI
{
    public class LoadPlaylistAction
    {
        public string FilePath { get; set; }
        public bool IsStartupLoad { get; set; } = false;
    }
}