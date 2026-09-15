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

            if (string.IsNullOrWhiteSpace(mediaLibraryPath) || !Path.IsPathFullyQualified(mediaLibraryPath))
            {
                throw new MediaLibraryNotConfiguredException();
            }

            var fullMediaLibraryPath = Path.GetFullPath(mediaLibraryPath);

            if (Path.IsPathFullyQualified(filePath) &&
                RelativeFilePathResolver.IsUnderDirectory(fullMediaLibraryPath, Path.GetFullPath(filePath)))
            {
                return Path.GetFullPath(filePath);
            }

            return CopyMediaOrPresentationIntoPlaylist(filePath, fullMediaLibraryPath);
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
