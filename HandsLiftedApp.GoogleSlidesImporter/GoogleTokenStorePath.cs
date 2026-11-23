using System;
using System.IO;
using Serilog;

namespace HandsLiftedApp.Importer.GoogleSlides
{
    /// <summary>
    /// Where the Google OAuth token (a <c>FileDataStore</c> folder named <c>token.json</c>) lives.
    /// It used to be the bare relative path "token.json", i.e. inside the process's working directory,
    /// which is the app's deploy folder: redeploying the app wiped it and forced a Google sign-in again.
    /// It now lives in the per-user app data folder, and a token found at the old location is copied
    /// over once so nobody has to sign in again after the change.
    /// </summary>
    public static class GoogleTokenStorePath
    {
        private static readonly object Gate = new();

        /// <summary>Same folder as the app's Constants.APP_DATA_DIR (including the test override).</summary>
        public static string DefaultDataDir() =>
            Environment.GetEnvironmentVariable("HANDSLIFTED_DATA_DIR") is { Length: > 0 } overrideDir
                ? overrideDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HandsLifted");

        /// <summary>The token folder to hand to <c>new FileDataStore(path, true)</c>.</summary>
        public static string Resolve() => Resolve(DefaultDataDir(), Environment.CurrentDirectory, AppContext.BaseDirectory);

        public static string Resolve(string dataDir, params string[] legacyBaseDirs)
        {
            var path = Path.Combine(dataDir, "token.json");
            lock (Gate)
            {
                if (Directory.Exists(path)) return path;

                foreach (var legacyBase in legacyBaseDirs)
                {
                    var legacy = Path.Combine(legacyBase, "token.json");
                    if (!Directory.Exists(legacy)) continue;
                    try
                    {
                        Directory.CreateDirectory(path);
                        foreach (var file in Directory.GetFiles(legacy))
                            File.Copy(file, Path.Combine(path, Path.GetFileName(file)), overwrite: false);
                        Log.Information("Moved the Google sign-in token from {Legacy} to {New}", legacy, path);
                    }
                    catch (Exception ex)
                    {
                        // Worst case the user signs in again; never fail the import over a migration.
                        Log.Warning(ex, "Could not move the Google sign-in token from {Legacy} to {New}", legacy, path);
                    }
                    break;
                }
            }
            return path;
        }
    }
}
