using System;
using System.IO;

namespace HandsLiftedApp.Core
{
    internal static class Constants
    {
        public static readonly string APP_DATA_DIR = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HandsLifted");

        public static readonly string APP_STATE_FILEPATH = Path.Combine(APP_DATA_DIR, "appstate.json");
        public static readonly string LIBRARY_CONFIG_FILEPATH = Path.Combine(APP_DATA_DIR, "library.yml");
        public static readonly string USER_CONFIG_FILEPATH = Path.Combine(APP_DATA_DIR, "HandsLiftedApp.UserConfig.json");

        public static readonly string[] SUPPORTED_SONG = { "txt", "xml" };
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        public static readonly string[] SUPPORTED_VIDEO = { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };
        public static readonly string[] SUPPORTED_IMAGE = { "bmp", "png", "jpg", "jpeg", "heic" };
        public static readonly string[] SUPPORTED_PDF = { "pdf" };
    }
}
