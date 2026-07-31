using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Serilog;

namespace HandsLiftedApp.Core.Utils
{
    public static class PortableAssetCopier
    {
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
