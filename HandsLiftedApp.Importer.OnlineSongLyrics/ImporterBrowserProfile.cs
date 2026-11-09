namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    /// <summary>
    /// The on-disk WebView2 profile (cookies, cache, local storage) used by the import browser window.
    /// </summary>
    public static class ImporterBrowserProfile
    {
        public static string UserDataFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HandsLifted", "WebView2");

        /// <summary>
        /// Deletes the whole profile folder, which signs the user out of every site. WebView2 keeps the
        /// folder locked while its browser process is alive (an open import window, or a process still
        /// shutting down after one closed), so this retries briefly before giving up.
        /// </summary>
        /// <returns>true if the folder is gone; false if it is still locked after all attempts.</returns>
        public static async Task<bool> TryClearAsync(string? folder = null, int attempts = 5, int retryDelayMs = 400)
        {
            folder ??= UserDataFolder;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                    }

                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (attempt == attempts)
                    {
                        Console.WriteLine($"[WebView] Could not clear browser data: {ex.Message}");
                        return false;
                    }

                    await Task.Delay(retryDelayMs);
                }
            }

            return false;
        }
    }
}
