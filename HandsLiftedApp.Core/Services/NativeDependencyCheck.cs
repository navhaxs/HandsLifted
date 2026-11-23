using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HandsLiftedApp.Core.Services
{
    /// <summary>A native library the app needs, and what stops working without it.</summary>
    public sealed record NativeDependency(string FileName, string Feature);

    /// <summary>A dialog to show the user once at startup.</summary>
    public sealed record StartupWarning(string Title, string Message);

    /// <summary>
    /// Startup check that the native DLLs the app ships beside its exe are really there.
    /// They are copied by project-reference <c>None</c> items, which a publish can silently drop
    /// (it did, under the .NET 11 preview SDK), and the failure is otherwise quiet: libmpv logs a
    /// Fatal and NDI swallows its DllNotFoundException. A missing file here always means a broken
    /// build or deploy, so it is worth one warning per launch.
    /// </summary>
    public static class NativeDependencyCheck
    {
        /// <summary>Windows only: other platforms resolve these libraries differently.</summary>
        public static readonly IReadOnlyList<NativeDependency> RequiredOnWindows = new[]
        {
            new NativeDependency("libmpv-2.dll", "video playback and motion backgrounds"),
            new NativeDependency("Processing.NDI.Lib.x64.dll", "NDI output"),
        };

        /// <summary>The required files that are not present in <paramref name="appDirectory"/>.</summary>
        public static IReadOnlyList<NativeDependency> FindMissing(string appDirectory, IEnumerable<NativeDependency> required) =>
            required.Where(d => !File.Exists(Path.Combine(appDirectory, d.FileName))).ToList();

        /// <summary>Null when nothing is missing, otherwise the dialog text listing each file and its consequence.</summary>
        public static StartupWarning? BuildWarning(string appDirectory, IReadOnlyList<NativeDependency> missing)
        {
            if (missing.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append("These files are missing from the app folder:\n").Append(appDirectory).Append("\n\n");
            foreach (var d in missing)
                sb.Append("• ").Append(d.FileName).Append(" - ").Append(d.Feature).Append('\n');
            sb.Append("\nThose features will not work. Reinstall the app, or copy the files next to it.");

            return new StartupWarning("Some app files are missing", sb.ToString());
        }

        /// <summary>Runs the check for the current platform and logs what it found.</summary>
        public static StartupWarning? CheckCurrentInstall()
        {
            if (!OperatingSystem.IsWindows()) return null;

            var dir = AppContext.BaseDirectory;
            var missing = FindMissing(dir, RequiredOnWindows);
            foreach (var d in missing)
                Serilog.Log.Warning("Native dependency missing: {File} ({Feature}) not found in {Dir}", d.FileName, d.Feature, dir);
            return BuildWarning(dir, missing);
        }
    }
}
