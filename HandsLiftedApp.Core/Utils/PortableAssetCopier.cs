using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Serilog;

namespace HandsLiftedApp.Core.Utils
{
    public class MediaLibraryNotConfiguredException : Exception
    {
        public MediaLibraryNotConfiguredException()
            : base("No Media Library folder is configured. Open Setup and choose one before adding media.")
        {
        }
    }

    public static class PortableAssetCopier
    {
        /// <summary>
        /// Resolves a file being added to a playlist against the configured Media Library folder.
        /// A file of a type this method doesn't route anywhere (e.g. a song XML file) passes
        /// through unchanged, same as <see cref="CopyMediaOrPresentationIntoPlaylist"/> — no
        /// library needed. A media/presentation file already inside the library is referenced in
        /// place (no copy); one from outside the library is copied in via
        /// <see cref="CopyMediaOrPresentationIntoPlaylist"/> so every playlist media reference ends
        /// up under the library.
        /// </summary>
        /// <exception cref="MediaLibraryNotConfiguredException">
        /// The file is a media/presentation type and no Media Library folder is configured.
        /// </exception>
        public static string ResolveOrCopyIntoMediaLibrary(string filePath, string? mediaLibraryPath)
        {
            if (!RequiresMediaLibrary(filePath))
            {
                return filePath;
            }

            var fullMediaLibraryPath = RequireMediaLibrary(mediaLibraryPath);

            return TryResolveInsideLibrary(filePath, fullMediaLibraryPath, out var resolved)
                ? resolved
                : CopyMediaOrPresentationIntoPlaylist(filePath, fullMediaLibraryPath);
        }

        /// <summary>
        /// Where pictures dropped or pasted into the custom slide editor are copied, relative to the Media Library.
        /// Kept apart from <c>Media/Images</c> so that clutter is easy to find and clean up.
        /// </summary>
        public static readonly string PastedSubfolder = Path.Combine("Media", "Pasted");

        /// <summary>
        /// Like <see cref="ResolveOrCopyIntoMediaLibrary"/>, but an image from outside the library is copied into
        /// <see cref="PastedSubfolder"/> (collisions handled by <see cref="CopyIntoSubfolder"/>). A file already
        /// anywhere under the library (even <c>Media/Images</c>) is referenced in place; a non-image passes through.
        /// </summary>
        /// <exception cref="MediaLibraryNotConfiguredException">
        /// The file is an image and no (fully qualified) Media Library folder is configured.
        /// </exception>
        public static string ResolveOrCopyIntoPastedFolder(string filePath, string? mediaLibraryPath)
        {
            if (!IsImageFile(filePath))
            {
                return filePath;
            }

            var fullMediaLibraryPath = RequireMediaLibrary(mediaLibraryPath);

            return TryResolveInsideLibrary(filePath, fullMediaLibraryPath, out var resolved)
                ? resolved
                : CopyIntoSubfolder(filePath, fullMediaLibraryPath, PastedSubfolder);
        }

        /// <summary>
        /// Saves a picture taken from the clipboard (already PNG-encoded) as
        /// <c>&lt;library&gt;/Media/Pasted/Pasted yyyy-MM-dd HH-mm-ss.png</c> (local time of <paramref name="now"/>, or
        /// now). An existing file is never overwritten: " (2)", " (3)"... is added before the extension.
        /// </summary>
        /// <returns>The full path of the new file.</returns>
        /// <exception cref="MediaLibraryNotConfiguredException">No (fully qualified) Media Library is configured.</exception>
        public static string SavePastedImage(byte[] pngBytes, string? mediaLibraryPath, DateTime? now = null) =>
            SavePastedImage(pngBytes, mediaLibraryPath, now, static (stream, bytes) => stream.Write(bytes, 0, bytes.Length));

        /// <summary>How many names (" (2)" ... " (N)") are tried before giving up.</summary>
        private const int MaxNameAttempts = 1000;

        /// <summary>The write step is injectable so tests can simulate a failing disk.</summary>
        internal static string SavePastedImage(byte[] pngBytes, string? mediaLibraryPath, DateTime? now,
            Action<FileStream, byte[]> write)
        {
            var fullMediaLibraryPath = RequireMediaLibrary(mediaLibraryPath);
            var destDir = Path.Combine(fullMediaLibraryPath, PastedSubfolder);
            Directory.CreateDirectory(destDir);

            var baseName = "Pasted " + (now ?? DateTime.Now).ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
            for (var n = 1; n <= MaxNameAttempts; n++)
            {
                var name = n == 1 ? $"{baseName}.png" : $"{baseName} ({n}).png";
                var destPath = Path.Combine(destDir, name);

                FileStream stream;
                try
                {
                    // CreateNew: fails instead of overwriting when the name is taken
                    stream = new FileStream(destPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (IOException) when (File.Exists(destPath))
                {
                    continue; // taken: try the next number
                }

                try
                {
                    using (stream) write(stream, pngBytes);
                    return destPath;
                }
                catch
                {
                    // a failed write (disk full, drive gone) is not a name collision: remove the partial file and fail
                    try { File.Delete(destPath); } catch (Exception) { /* best effort */ }
                    throw;
                }
            }

            throw new IOException($"Could not find a free name for the pasted picture in {destDir}");
        }

        /// <summary>A file already under the library is referenced in place (its full path), never copied.</summary>
        private static bool TryResolveInsideLibrary(string filePath, string fullMediaLibraryPath, out string resolved)
        {
            if (Path.IsPathFullyQualified(filePath) &&
                RelativeFilePathResolver.IsUnderDirectory(fullMediaLibraryPath, Path.GetFullPath(filePath)))
            {
                resolved = Path.GetFullPath(filePath);
                return true;
            }

            resolved = filePath;
            return false;
        }

        /// <summary>Whether the path has one of the <see cref="Constants.SUPPORTED_IMAGE"/> extensions (any case).</summary>
        public static bool IsImageFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            return ext.Length > 0 && Constants.SUPPORTED_IMAGE.Contains(ext);
        }

        private static string RequireMediaLibrary(string? mediaLibraryPath)
        {
            if (string.IsNullOrWhiteSpace(mediaLibraryPath) || !Path.IsPathFullyQualified(mediaLibraryPath))
            {
                throw new MediaLibraryNotConfiguredException();
            }

            return Path.GetFullPath(mediaLibraryPath);
        }

        private static bool RequiresMediaLibrary(string filePath)
        {
            var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            return Constants.SUPPORTED_IMAGE.Contains(ext)
                   || Constants.SUPPORTED_VIDEO.Contains(ext)
                   || Constants.SUPPORTED_PDF.Contains(ext)
                   || Constants.SUPPORTED_POWERPOINT.Contains(ext);
        }

        public static string CopyIntoSubfolder(string sourceFilePath, string playlistWorkingDirectory, string relativeSubfolder)
        {
            // A playlist that has never been saved still carries the class-default relative
            // working directory, which would resolve against Environment.CurrentDirectory —
            // writing copies somewhere arbitrary in a dev run, or throwing
            // UnauthorizedAccessException under Program Files in an installed build. Leave the
            // source path alone instead; the next save (which sets a real working directory)
            // is when copying can safely happen.
            if (string.IsNullOrWhiteSpace(playlistWorkingDirectory)
                || !Path.IsPathFullyQualified(playlistWorkingDirectory))
            {
                Log.Warning(
                    "Skipping copy of {SourceFilePath} into playlist folder: PlaylistWorkingDirectory {PlaylistWorkingDirectory} is not a fully-qualified path (playlist not saved yet?)",
                    sourceFilePath, playlistWorkingDirectory);
                return sourceFilePath;
            }

            var destDir = Path.Combine(playlistWorkingDirectory, relativeSubfolder);
            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = Path.Combine(destDir, fileName);

            if (File.Exists(destPath))
            {
                if (FilesAreIdentical(sourceFilePath, destPath))
                {
                    return destPath;
                }

                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var suffix = ComputeFileHash(sourceFilePath)[..8];
                destPath = Path.Combine(destDir, $"{nameNoExt}_{suffix}{ext}");

                if (File.Exists(destPath) && FilesAreIdentical(sourceFilePath, destPath))
                {
                    return destPath;
                }
            }

            File.Copy(sourceFilePath, destPath, overwrite: true);
            return destPath;
        }

        public static string CopyMediaOrPresentationIntoPlaylist(string filePath, string playlistWorkingDirectory)
        {
            var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            if (Constants.SUPPORTED_IMAGE.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, Path.Combine("Media", "Images"));
            }

            if (Constants.SUPPORTED_VIDEO.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, Path.Combine("Media", "Video"));
            }

            if (Constants.SUPPORTED_PDF.Contains(ext) || Constants.SUPPORTED_POWERPOINT.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, "Sources");
            }

            return filePath;
        }

        private static bool FilesAreIdentical(string pathA, string pathB)
        {
            if (new FileInfo(pathA).Length != new FileInfo(pathB).Length)
            {
                return false;
            }

            return ComputeFileHash(pathA) == ComputeFileHash(pathB);
        }

        private static string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
