using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;
using Serilog;

namespace HandsLiftedApp.Core.Services;

public static class NativePowerPointImportService
{
    private static string? _expectedHelperHash;
    private static IntPtr _jobHandle = IntPtr.Zero;
    private static Process? _currentHelperProcess;

    [SupportedOSPlatform("windows")]
    public static void Initialize(string? expectedHelperHash = null)
    {
        _expectedHelperHash = expectedHelperHash == "HELPER_NOT_BUILT" ? null : expectedHelperHash;
        _jobHandle = CreateJobWithKillOnClose();
        Log.Information("NativePowerPointImportService initialized. Hash verification: {Enabled}",
            _expectedHelperHash != null);
    }

    [SupportedOSPlatform("windows")]
    public static ImportStats RunImport(ImportTask task, IProgress<ImportStats>? progress = null)
    {
        var helperPath = Path.Combine(AppContext.BaseDirectory,
            "PowerPointInteropHost", "HandsLiftedApp.Importer.PowerPointInteropHost.exe");

        Log.Information("Launching PowerPoint interop helper: {HelperPath}", helperPath);

        if (!File.Exists(helperPath))
            throw new FileNotFoundException("PowerPoint interop helper not found.", helperPath);

        if (_expectedHelperHash != null)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(helperPath);
            var actual = Convert.ToHexString(sha.ComputeHash(fs));
            if (!actual.Equals(_expectedHelperHash, StringComparison.OrdinalIgnoreCase))
                throw new SecurityException(
                    $"Helper exe hash mismatch. Expected {_expectedHelperHash}, got {actual}. " +
                    "Binary may be tampered or out of date with this build.");
        }

        var pipeName = "HandsLifted.PowerPointInterop." + Guid.NewGuid().ToString("N");
        Log.Debug("Native PowerPoint import pipe name: {PipeName}", pipeName);

        using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.None);

        var psi = new ProcessStartInfo
        {
            FileName = helperPath,
            Arguments = pipeName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            lock (stderr) stderr.AppendLine(e.Data);
            Log.Warning("PowerPoint helper stderr: {Line}", e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        Log.Information("PowerPoint helper process started (PID {Pid})", process.Id);

        if (_jobHandle != IntPtr.Zero)
            if (!AssignProcessToJobObject(_jobHandle, process.Handle))
                Log.Warning("AssignProcessToJobObject failed (err={Err}); helper may survive main-app crash",
                    Marshal.GetLastWin32Error());

        _currentHelperProcess = process;

        try
        {
            Log.Debug("Waiting for helper to connect to pipe...");
            try
            {
                using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                pipe.WaitForConnectionAsync(connectCts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                var exited = process.HasExited;
                Log.Error(
                    "PowerPoint helper did not connect to pipe within 15s (processExited={Exited}, exitCode={Code}). Stderr: {Stderr}",
                    exited, exited ? process.ExitCode : (int?)null, stderr.ToString());
                throw new TimeoutException("PowerPoint helper did not connect to the IPC pipe in time.");
            }
            Log.Debug("Helper connected to pipe");

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            writer.WriteLine(JsonSerializer.Serialize(task));
            Log.Debug("Sent import task to helper: {InputFile} -> {OutputDirectory}",
                task.InputFile, task.OutputDirectory);

            ImportStats? finalStats = null;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                try
                {
                    var stats = JsonSerializer.Deserialize<ImportStats>(line);
                    if (stats == null) continue;
                    Log.Debug("Helper progress: {Status} {Pct}% - {Msg}",
                        stats.JobStatus, stats.JobPercentage, stats.StatusMessage);
                    progress?.Report(stats);
                    finalStats = stats;
                    if (stats.JobStatus == ImportStats.JobStatusEnum.CompletionSuccess ||
                        stats.JobStatus == ImportStats.JobStatusEnum.CompletionFailure)
                        break;
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Failed to deserialize ImportStats from helper: {Line}", line);
                }
            }

            if (finalStats == null)
                Log.Error("Helper pipe closed before reporting a completion status. Stderr: {Stderr}",
                    stderr.ToString());

            process.WaitForExit(30_000);
            if (!process.HasExited)
            {
                Log.Warning("Helper process did not exit within 30s after completion; killing it");
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            else
            {
                Log.Information("Helper process exited with code {Code}", process.ExitCode);
            }

            return finalStats ?? new ImportStats { JobStatus = ImportStats.JobStatusEnum.CompletionFailure };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Native PowerPoint import failed. Helper stderr: {Stderr}", stderr.ToString());
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ImportStats { JobStatus = ImportStats.JobStatusEnum.CompletionFailure };
        }
        finally
        {
            _currentHelperProcess = null;
        }
    }

    public static void Shutdown()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _currentHelperProcess?.Kill(entireProcessTree: true);
            _currentHelperProcess = null;
        }
        catch (Exception ex) { Log.Warning(ex, "Error killing helper process on shutdown"); }

        if (_jobHandle != IntPtr.Zero)
        {
            CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }
    }

    // Win32 Job Object — ensures all helper processes die when main app exits (even on crash)
    // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
    // JobObjectExtendedLimitInformation = 9

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

    [DllImport("kernel32.dll")]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [SupportedOSPlatform("windows")]
    private static IntPtr CreateJobWithKillOnClose()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            Log.Warning("CreateJobObject failed (hr={Hr})", Marshal.GetLastWin32Error());
            return IntPtr.Zero;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        int size = Marshal.SizeOf(info);
        if (!SetInformationJobObject(job, 9, ref info, size))
            Log.Warning("SetInformationJobObject failed (hr={Hr})", Marshal.GetLastWin32Error());

        return job;
    }
}
