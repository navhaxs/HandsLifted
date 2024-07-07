namespace HandsLiftedApp.Core
{
    internal static class Constants
    {
        public const string APP_STATE_FILEPATH = "appstate.json";
        public const string LIBRARY_CONFIG_FILEPATH = "library.yml";
        
        public static readonly string[] SUPPORTED_SONG = { "txt", "xml" };
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        public static readonly string[] SUPPORTED_VIDEO = { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };
        public static readonly string[] SUPPORTED_IMAGE = { "bmp", "png", "jpg", "jpeg" };
        public static readonly string[] SUPPORTED_PDF= { "pdf" };
    }
}
