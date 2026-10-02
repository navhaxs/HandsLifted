using System;
using System.Collections.Generic;
using System.IO;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public sealed record SlideThemeMigrationResult(
        int Imported,
        int SkippedExisting,
        Guid? DefaultSongThemeId,
        Guid? DefaultSongMotionThemeId,
        Guid? DefaultScriptureThemeId,
        bool AppDefaultsChanged,
        bool NeedsResave,
        bool LibraryUnavailable = false);

    /// <summary>
    /// Copies themes embedded in a legacy playlist into the app-level SlideThemeLibrary.
    /// Never overwrites a library theme that has the same Id.
    /// </summary>
    public static class SlideThemeMigration
    {
        public static SlideThemeMigrationResult Migrate(
            IEnumerable<BaseSlideTheme> embedded,
            string playlistDirectory,
            string? mediaLibraryPath,
            SlideThemeLibrary library,
            AppPreferencesViewModel prefs,
            Guid? songId, Guid? motionId, Guid? scriptureId)
        {
            int imported = 0, skipped = 0;
            foreach (var theme in embedded)
            {
                if (library.Contains(theme.Id))
                {
                    skipped++;
                    Log.Information("Slide theme migration - {Name} ({Id}) already in library, skipping", theme.Name, theme.Id);
                    continue;
                }

                theme.BackgroundGraphicFilePath =
                    ImportAsset(RelativeFilePathResolver.ToAbsolutePath(playlistDirectory, theme.BackgroundGraphicFilePath),
                        mediaLibraryPath);
                theme.DefaultMotionBackgroundVideoPath =
                    ImportAsset(RelativeFilePathResolver.ToAbsoluteMediaPath(mediaLibraryPath, playlistDirectory,
                        theme.DefaultMotionBackgroundVideoPath), mediaLibraryPath);

                library.Themes.Add(theme);
                library.SaveNow(theme.Id);
                imported++;
            }

            // No usable themes folder: the themes live in memory for this session only. Saving the
            // playlist would drop them (Designs is no longer serialized), so do not request a resave
            // and do not move playlist defaults to app level (they would point at unsaved themes).
            if (library.Folder == null && imported + skipped > 0)
            {
                Log.Warning("Slide theme migration - themes folder unavailable, {Count} themes kept in memory only", imported + skipped);
                return new SlideThemeMigrationResult(imported, skipped, songId, motionId, scriptureId,
                    false, false, LibraryUnavailable: true);
            }

            var appChanged = false;
            var newSong = Adopt(songId, prefs.DefaultSongThemeId, id => prefs.DefaultSongThemeId = id, library, ref appChanged);
            var newMotion = Adopt(motionId, prefs.DefaultSongMotionThemeId, id => prefs.DefaultSongMotionThemeId = id, library, ref appChanged);
            var newScripture = Adopt(scriptureId, prefs.DefaultScriptureThemeId, id => prefs.DefaultScriptureThemeId = id, library, ref appChanged);

            var needsResave = imported + skipped > 0
                              || newSong != songId || newMotion != motionId || newScripture != scriptureId;

            return new SlideThemeMigrationResult(imported, skipped, newSong, newMotion, newScripture, appChanged, needsResave);
        }

        private static Guid? Adopt(Guid? playlistId, Guid? appId, Action<Guid> setApp, SlideThemeLibrary library,
            ref bool appChanged)
        {
            if (!playlistId.HasValue) return null;
            if (!library.Contains(playlistId.Value)) return playlistId; // dangling: leave as is
            if (!appId.HasValue)
            {
                setApp(playlistId.Value);
                appChanged = true;
                return null;
            }
            return appId == playlistId ? null : playlistId;
        }

        // Copies a theme asset into the Media Library. Any failure keeps the absolute path so the
        // theme still imports (a missing or unconfigured asset must never block opening a playlist).
        private static string? ImportAsset(string? absolutePath, string? mediaLibraryPath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)
                || absolutePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                return absolutePath;

            if (!File.Exists(absolutePath))
            {
                Log.Warning("Slide theme migration - asset not found, keeping path: {Path}", absolutePath);
                return absolutePath;
            }

            try
            {
                return PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(absolutePath, mediaLibraryPath);
            }
            catch (MediaLibraryNotConfiguredException)
            {
                Log.Warning("Slide theme migration - no Media Library configured, keeping absolute path: {Path}", absolutePath);
                return absolutePath;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Slide theme migration - failed to copy asset, keeping path: {Path}", absolutePath);
                return absolutePath;
            }
        }
    }
}
