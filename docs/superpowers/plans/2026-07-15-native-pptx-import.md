# Native PowerPoint Import via Interop Host Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Syncfusion PPTX→PDF→PNG import path with an optional native Windows path that drives Microsoft PowerPoint via COM (NetOffice) in a sandboxed helper subprocess, with automatic fallback to Syncfusion on non-Windows or when disabled.

**Architecture:** A static `PowerPointImportSettings` flag (mirroring `ThumbnailEngineSettings`) is read by `PowerPointPresentationItemInstance.Sync()` to branch between two code paths. The native path is implemented in `NativePowerPointImportService`, a static service that launches `HandsLiftedApp.Importer.PowerPointInteropHost.exe` per-import, communicates via a unique named pipe (pipe name passed as CLI arg), and attaches each helper process to a Win32 Job Object so orphans are killed automatically on main-app crash. The helper exe hash is baked at compile time via an MSBuild inline C# task and verified before each launch.

**Tech Stack:** C# / .NET 8, System.IO.Pipes, System.Text.Json, Win32 Job Objects (P/Invoke on kernel32.dll), MSBuild RoslynCodeTaskFactory, NetOffice.PowerPoint (in helper), Syncfusion.Presentation (fallback)

## Global Constraints

- Native import path: Windows-only (`OperatingSystem.IsWindows()` runtime guard + `[SupportedOSPlatform("windows")]` attributes)
- `HandsLiftedApp.Core` targets `net8.0` (not `net8.0-windows`) — no `#if WINDOWS` preprocessor, use runtime OS checks and SupportedOSPlatform attributes
- `HandsLiftedApp.Importer.PowerPointInteropHost` targets `net8.0-windows`, outputs a WinExe
- Feature flag default: `false` on all platforms (explicit opt-in required via Setup > Integrations)
- Follow `ThumbnailEngineSettings` pattern for the static settings class
- Follow `MotionBackgroundService` pattern for the static service class
- No changes to Syncfusion path — it remains the fallback, unchanged
- Pipe name must be unique per invocation (GUID suffix) to avoid conflicts
- Helper is single-job: it exits after completing one import. Each `Sync()` call spawns a fresh process.

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `HandsLiftedApp.Core/Utils/PowerPointImportSettings.cs` | Static feature flag, mirrors `ThumbnailEngineSettings` |
| Create | `HandsLiftedApp.Core/Services/NativePowerPointImportService.cs` | Pipe server, helper process launch, Win32 Job Object, hash verification |
| Modify | `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj` | OrderingProjectReference to InteropHost, MSBuild hash-embed target |
| Modify | `HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj` | Copy helper exe to output dir after build |
| Modify | `HandsLiftedApp.Importer.PowerPointInteropHost/Program.cs` | Accept pipe name from `args[0]` instead of hardcoded string |
| Modify | `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs` | Add `UseNativePowerPointImport` persisted bool |
| Modify | `HandsLiftedApp.Core/Globals.cs` | Initialize + shutdown `NativePowerPointImportService`; sync flag from prefs |
| Modify | `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs` | Branch Sync() between native and Syncfusion paths |
| Modify | `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml` | Add toggle in Integrations tab (Windows-only visible) |

---

### Task 1: `PowerPointImportSettings` static flag

**Files:**
- Create: `HandsLiftedApp.Core/Utils/PowerPointImportSettings.cs`

**Interfaces:**
- Produces: `PowerPointImportSettings.UseNativeInterop` — `bool` property, gets/sets the opt-in flag

- [ ] **Step 1: Create the file**

```csharp
using System.Runtime.InteropServices;

namespace HandsLiftedApp.Core.Utils;

public static class PowerPointImportSettings
{
    // Windows-only native COM path. Default false = opt-in required.
    // Non-Windows: always false regardless of setter (COM unavailable).
    public static bool UseNativeInterop
    {
        get => _useNativeInterop && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        set => _useNativeInterop = value;
    }

    private static bool _useNativeInterop = false;
}
```

- [ ] **Step 2: Build to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```
git add HandsLiftedApp.Core/Utils/PowerPointImportSettings.cs
git commit -m "feat: add PowerPointImportSettings feature flag (opt-in native COM import)"
```

---

### Task 2: `AppPreferencesViewModel` — persisted flag

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs`

**Interfaces:**
- Consumes: existing `[DataMember]` pattern in `AppPreferencesViewModel`
- Produces: `AppPreferencesViewModel.UseNativePowerPointImport` — `bool` [DataMember], persisted to `appstate.json`

- [ ] **Step 1: Add the property**

In `AppPreferencesViewModel.cs`, after `_enableDebugStats` property (around line 78), add:

```csharp
private bool _useNativePowerPointImport = false;
[DataMember]
public bool UseNativePowerPointImport
{
    get => _useNativePowerPointImport;
    set => this.RaiseAndSetIfChanged(ref _useNativePowerPointImport, value);
}
```

- [ ] **Step 2: Build to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```
git add HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs
git commit -m "feat: add UseNativePowerPointImport to AppPreferencesViewModel"
```

---

### Task 3: Integrations UI toggle in Setup window

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml`

**Interfaces:**
- Consumes: `AppPreferencesViewModel.UseNativePowerPointImport` (Task 2), `Globals.Instance.AppPreferences`
- Produces: Checkbox in Integrations tab that toggles the flag, visible only on Windows

The Integrations tab `<StackPanel>` starts at line 231 of `SetupWindow.axaml`. Add the following block **before** the Google Slides section (before `<TextBlock Text="Google Slides API Key" />`):

- [ ] **Step 1: Add the toggle**

Add this block immediately after `<StackPanel Margin="12">` on line 231:

```xml
<!-- Native PowerPoint Import (Windows only) -->
<StackPanel IsVisible="{OnPlatform Default=False, Windows=True}">
    <TextBlock
        FontSize="13"
        FontWeight="SemiBold"
        Margin="0,0,0,4"
        Text="PowerPoint Import" />
    <CheckBox
        Content="Use native PowerPoint import (requires Microsoft Office installed)"
        IsChecked="{Binding Source={x:Static app:Globals.Instance}, Path=AppPreferences.UseNativePowerPointImport}" />
    <TextBlock
        Foreground="{DynamicResource SystemColorGrayTextBrush}"
        Margin="22,2,0,0"
        TextWrapping="Wrap"
        Text="Uses COM automation via Microsoft PowerPoint for higher-fidelity slide export. Falls back to built-in converter when disabled." />
    <Grid Margin="0,8,0,0" />
</StackPanel>
```

- [ ] **Step 2: Build and visually verify in app on Windows**

```powershell
dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj -c Debug
```

Run the app, open Setup > Integrations. Confirm checkbox appears on Windows. Toggle it and close/reopen Setup — it should persist (app state saved on exit).

- [ ] **Step 3: Commit**

```
git add HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml
git commit -m "feat: add native PowerPoint import toggle in Setup > Integrations (Windows-only)"
```

---

### Task 4: MSBuild — hash embedding + helper deployment

This task has two sub-parts:
- A) Embed the helper exe hash into `HandsLiftedApp.Core` at compile time
- B) Copy the helper exe to `HandsLiftedApp.Desktop` output after build
- C) Update the helper to accept pipe name from CLI args (needed by Task 5)

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
- Modify: `HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj`
- Modify: `HandsLiftedApp.Importer.PowerPointInteropHost/Program.cs`

**Interfaces:**
- Produces: `HandsLiftedApp.Core.BuildConstants.HelperExeHash` — `const string` (SHA-256 hex of helper exe, or `null` if helper not yet built)
- Produces: `HandsLiftedApp.Importer.PowerPointInteropHost.exe` copied to Desktop output directory

**Part A: Hash embedding in `HandsLiftedApp.Core.csproj`**

- [ ] **Step 1: Add ordering reference + hash-embed target to `HandsLiftedApp.Core.csproj`**

Add inside `<Project>`, before the closing `</Project>` tag (after all existing `<ItemGroup>` and `<Target>` elements):

```xml
<!-- Build ordering: InteropHost must compile before Core so hash target can read its exe -->
<ItemGroup>
  <ProjectReference
    Include="..\HandsLiftedApp.Importer.PowerPointInteropHost\HandsLiftedApp.Importer.PowerPointInteropHost.csproj"
    ReferenceOutputAssembly="false"
    SkipGetTargetFrameworkProperties="true" />
</ItemGroup>

<PropertyGroup>
  <_InteropHostExe>$(MSBuildThisFileDirectory)..\HandsLiftedApp.Importer.PowerPointInteropHost\bin\$(Configuration)\net8.0-windows\HandsLiftedApp.Importer.PowerPointInteropHost.exe</_InteropHostExe>
  <_HashFile>$(IntermediateOutputPath)HelperHash.g.cs</_HashFile>
</PropertyGroup>

<UsingTask TaskName="WriteHelperHashFile"
           TaskFactory="RoslynCodeTaskFactory"
           AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
  <ParameterGroup>
    <ExePath ParameterType="System.String" Required="true" />
    <OutputPath ParameterType="System.String" Required="true" />
  </ParameterGroup>
  <Task>
    <Using Namespace="System.IO" />
    <Using Namespace="System.Security.Cryptography" />
    <Code Type="Fragment" Language="cs"><![CDATA[
      string hash = Exists(ExePath)
          ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExePath)))
          : "HELPER_NOT_BUILT";
      File.WriteAllText(OutputPath,
          "namespace HandsLiftedApp.Core {" +
          "  internal static class BuildConstants {" +
          "    public const string HelperExeHash = \"" + hash + "\";" +
          "  }" +
          "}");
    ]]></Code>
  </Task>
</UsingTask>

<Target Name="EmbedHelperHash" BeforeTargets="CoreCompile">
  <MakeDir Directories="$(IntermediateOutputPath)" />
  <WriteHelperHashFile ExePath="$(_InteropHostExe)" OutputPath="$(_HashFile)" />
  <ItemGroup>
    <Compile Include="$(_HashFile)" />
  </ItemGroup>
</Target>
```

Note: `Exists()` is an MSBuild intrinsic function. The C# inline task uses `File.Exists` via `System.IO`. The `Exists(ExePath)` call uses .NET's `File.Exists` in the C# fragment context. Verify this compiles correctly.

Actually, correct the C# fragment — use `File.Exists` not bare `Exists`:

```xml
    <Code Type="Fragment" Language="cs"><![CDATA[
      bool helperExists = File.Exists(ExePath);
      string hash = helperExists
          ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExePath)))
          : "HELPER_NOT_BUILT";
      File.WriteAllText(OutputPath,
          "namespace HandsLiftedApp.Core {" +
          "  internal static class BuildConstants {" +
          "    public const string HelperExeHash = \"" + hash + "\";" +
          "  }" +
          "}");
    ]]></Code>
```

- [ ] **Step 2: Build Core to verify hash generation**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: Build succeeded. Check the generated file exists:

```powershell
Get-Content "HandsLiftedApp.Core/obj/Debug/net8.0/HelperHash.g.cs"
```

Expected output: a file containing `public const string HelperExeHash = "HELPER_NOT_BUILT";` (or a real hash if the interop host was already built).

**Part B: Copy helper exe to Desktop output**

- [ ] **Step 3: Add CopyInteropHost target to `HandsLiftedApp.Desktop.csproj`**

Add before `</Project>`:

```xml
<PropertyGroup>
  <_InteropHostSrc>$(MSBuildThisFileDirectory)..\HandsLiftedApp.Importer.PowerPointInteropHost\bin\$(Configuration)\net8.0-windows\HandsLiftedApp.Importer.PowerPointInteropHost.exe</_InteropHostSrc>
</PropertyGroup>

<Target Name="CopyInteropHost" AfterTargets="Build" Condition="Exists('$(_InteropHostSrc)')">
  <Copy SourceFiles="$(_InteropHostSrc)" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
  <Message Text="Copied InteropHost to $(OutputPath)" Importance="normal" />
</Target>
```

**Part C: Update helper to accept pipe name from args**

- [ ] **Step 4: Modify `HandsLiftedApp.Importer.PowerPointInteropHost/Program.cs`**

Change line 20 (the `NamedPipeClientStream` constructor) from:

```csharp
_pipe = new NamedPipeClientStream(".", "HandsLifted.PowerPointInterop", PipeDirection.InOut);
```

To:

```csharp
var pipeName = args.Length > 0 ? args[0] : "HandsLifted.PowerPointInterop";
_pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
```

- [ ] **Step 5: Build everything to verify**

```powershell
dotnet build HandsLiftedApp.Importer.PowerPointInteropHost/HandsLiftedApp.Importer.PowerPointInteropHost.csproj -c Debug
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj -c Debug
```

Then verify hash is now a real SHA-256:

```powershell
Get-Content "HandsLiftedApp.Core/obj/Debug/net8.0/HelperHash.g.cs"
```

Expected: `public const string HelperExeHash = "A3F2B1..."` (64-char hex string, not `HELPER_NOT_BUILT`).

Verify helper copied to Desktop output:

```powershell
Test-Path "HandsLiftedApp.Desktop/bin/Debug/net8.0/HandsLiftedApp.Importer.PowerPointInteropHost.exe"
```

Expected: `True`

- [ ] **Step 6: Commit**

```
git add HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
git add HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj
git add HandsLiftedApp.Importer.PowerPointInteropHost/Program.cs
git commit -m "build: embed helper exe hash at compile time, copy helper to Desktop output"
```

---

### Task 5: `NativePowerPointImportService`

**Files:**
- Create: `HandsLiftedApp.Core/Services/NativePowerPointImportService.cs`

**Interfaces:**
- Consumes: `ImportTask`, `ImportStats` from `HandsLiftedApp.Importer.FileFormatConvertTaskData`
- Consumes: `BuildConstants.HelperExeHash` from Task 4
- Produces: `NativePowerPointImportService.Initialize(string? expectedHash)` — call once at startup on Windows
- Produces: `NativePowerPointImportService.RunImport(ImportTask, IProgress<ImportStats>?)` — blocking, call from background thread
- Produces: `NativePowerPointImportService.Shutdown()` — call on app exit

- [ ] **Step 1: Create the service file**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
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
            "HandsLiftedApp.Importer.PowerPointInteropHost.exe");

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

        using var process = new Process { StartInfo = psi };
        process.Start();

        if (_jobHandle != IntPtr.Zero)
            AssignProcessToJobObject(_jobHandle, process.Handle);

        _currentHelperProcess = process;

        try
        {
            pipe.WaitForConnection();

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            writer.WriteLine(JsonSerializer.Serialize(task));

            ImportStats? finalStats = null;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                try
                {
                    var stats = JsonSerializer.Deserialize<ImportStats>(line);
                    if (stats == null) continue;
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

            process.WaitForExit(30_000);
            return finalStats ?? new ImportStats { JobStatus = ImportStats.JobStatusEnum.CompletionFailure };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Native PowerPoint import failed");
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
```

- [ ] **Step 2: Add `AllowUnsafeBlocks` is already enabled in Core.csproj — verify P/Invoke compiles**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: Build succeeded with 0 errors. Warnings about `[SupportedOSPlatform]` are expected and acceptable — callers are guarded.

- [ ] **Step 3: Commit**

```
git add HandsLiftedApp.Core/Services/NativePowerPointImportService.cs
git commit -m "feat: add NativePowerPointImportService with Win32 job object and named pipe IPC"
```

---

### Task 6: Update `PowerPointPresentationItemInstance.Sync()`

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs`

**Interfaces:**
- Consumes: `PowerPointImportSettings.UseNativeInterop` (Task 1), `NativePowerPointImportService.RunImport` (Task 5)
- Consumes: `ImportTask.ExportFileFormatType.PNG` — native path requests PNG directly (COM exports slide-by-slide to PNG, skipping the PDF intermediate step)
- Produces: updated `Sync()` that branches on the flag

The current `Sync()` body (lines 110-180) does:
1. `PresentationFileFormatConverter.Run()` → PPTX to PDF (Syncfusion)
2. `ConvertPDF.Convert()` → PDF to PNG slides (PDFium)

The native path skips both: COM exports PNGs directly. The helper writes `slide_1.png`, `slide_2.png`, etc. to the output directory. The downstream `Directory.GetFiles()` + slide-generation code is identical for both paths.

- [ ] **Step 1: Add `using` for the new types**

At the top of `PowerPointPresentationItemInstance.cs`, ensure these usings are present (some already exist):

```csharp
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Utils;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
```

- [ ] **Step 2: Extract current Sync() body to `SyncViaSyncfusion()`**

Rename the existing inner `Callback = () => { ... }` lambda body to a named private method. Replace the `Sync()` method with:

```csharp
public void Sync()
{
    if (IsBusy) return;
    IsBusy = true;

    ImportWorkerThread.priorityQueue.Add(new ImportWorkerThread.BackgroundWorkRequest()
    {
        Callback = () =>
        {
            if (PowerPointImportSettings.UseNativeInterop && OperatingSystem.IsWindows())
                SyncViaNativeInterop();
            else
                SyncViaSyncfusion();
        }
    });
}

private void SyncViaSyncfusion()
{
    lock (syncSlidesLock)
    {
        try
        {
            DateTime now = DateTime.Now;
            string fileName = Path.GetFileName(SourcePresentationFile);

            string targetDirectory = Path.Join(ParentPlaylist.PlaylistWorkingDirectory,
                FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                now.ToString("yyyy-MM-dd-HH-mm-ss"));
            Directory.CreateDirectory(targetDirectory);

            Log.Debug($"Importing PowerPoint file (Syncfusion): {SourcePresentationFile}");
            PresentationFileFormatConverter.Run(new ImportTask
            {
                InputFile = SourcePresentationFile,
                OutputDirectory = targetDirectory,
                ExportFileFormat = ImportTask.ExportFileFormatType.PDF
            }, new ImportTaskReporter(stats => { }));

            Log.Debug($"Converting PDF to slides: {SourcePresentationFile}");
            ConvertPDF.Convert(new ImportTask
            {
                InputFile = SourcePresentationFile + ".pdf", // TODO: use output path from above step
                OutputDirectory = targetDirectory,
                ExportFileFormat = ImportTask.ExportFileFormatType.PDF
            }, new ImportTaskReporter(stats => { }));

            ApplySlidesFromDirectory(targetDirectory);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error importing PowerPoint file via Syncfusion");
        }
        IsBusy = false;
    }
}

[SupportedOSPlatform("windows")]
private void SyncViaNativeInterop()
{
    lock (syncSlidesLock)
    {
        try
        {
            DateTime now = DateTime.Now;
            string fileName = Path.GetFileName(SourcePresentationFile);

            string targetDirectory = Path.Join(ParentPlaylist.PlaylistWorkingDirectory,
                FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                now.ToString("yyyy-MM-dd-HH-mm-ss"));
            Directory.CreateDirectory(targetDirectory);

            Log.Debug($"Importing PowerPoint file (native COM): {SourcePresentationFile}");

            var result = NativePowerPointImportService.RunImport(
                new ImportTask
                {
                    InputFile = SourcePresentationFile,
                    OutputDirectory = targetDirectory,
                    ExportFileFormat = ImportTask.ExportFileFormatType.PNG
                },
                new ImportTaskReporter(stats =>
                {
                    Log.Debug("Native import progress: {Pct}% — {Status}",
                        stats.JobPercentage, stats.StatusMessage);
                }));

            if (result.JobStatus != ImportStats.JobStatusEnum.CompletionSuccess)
            {
                Log.Error("Native PowerPoint import failed with status {Status}", result.JobStatus);
                IsBusy = false;
                return;
            }

            ApplySlidesFromDirectory(targetDirectory);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error importing PowerPoint file via native interop");
        }
        IsBusy = false;
    }
}

private void ApplySlidesFromDirectory(string targetDirectory)
{
    var newItems = new TrulyObservableCollection<GroupItem>();
    foreach (var convertedFilePath in Directory.GetFiles(targetDirectory)
                 .OrderBy(x => x, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
    {
        // Skip non-image files (e.g. intermediate PDFs from Syncfusion path)
        var ext = Path.GetExtension(convertedFilePath).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

        newItems.Add(new MediaItem { SourceMediaFilePath = convertedFilePath });
    }

    Items = newItems;

    Log.Debug("Generating slides");
    GenerateSlides();

    Log.Debug("Import OK");
    LastSyncDateTime = DateTime.Now;
}
```

- [ ] **Step 3: Build to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```
git add HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs
git commit -m "feat: branch PowerPoint Sync() between native COM and Syncfusion fallback paths"
```

---

### Task 7: `Globals` lifecycle wiring

**Files:**
- Modify: `HandsLiftedApp.Core/Globals.cs`

**Interfaces:**
- Consumes: `NativePowerPointImportService.Initialize(string?)` (Task 5), `PowerPointImportSettings.UseNativeInterop` setter (Task 1), `AppPreferencesViewModel.UseNativePowerPointImport` (Task 2), `BuildConstants.HelperExeHash` (Task 4)
- Consumes: `NativePowerPointImportService.Shutdown()` (Task 5)

- [ ] **Step 1: Add initialization to `OnStartup()`**

In `Globals.cs`, in `OnStartup()` after the `AppPreferences` load block (around line 91, after the `ThumbnailEngineSettings.UseMpvEngine = true;` line), add:

```csharp
// Native PowerPoint import (Windows-only)
if (OperatingSystem.IsWindows())
{
    PowerPointImportSettings.UseNativeInterop = AppPreferences.UseNativePowerPointImport;
    NativePowerPointImportService.Initialize(BuildConstants.HelperExeHash);
}
```

Add required usings at the top of `Globals.cs` if not already present:

```csharp
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Utils;
```

- [ ] **Step 2: Wire AppPreferences change to the flag**

After the initialization block, subscribe to preference changes so toggling the checkbox takes effect immediately without restart:

```csharp
if (OperatingSystem.IsWindows())
{
    AppPreferences.WhenAnyValue(p => p.UseNativePowerPointImport)
        .Subscribe(val => PowerPointImportSettings.UseNativeInterop = val);
}
```

- [ ] **Step 3: Add shutdown to `OnShutdown()`**

In `OnShutdown()`, after `IsShuttingDown = true;` (line 106), add:

```csharp
NativePowerPointImportService.Shutdown();
```

- [ ] **Step 4: Build full solution to confirm no errors**

```powershell
dotnet build HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj -c Debug
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```
git add HandsLiftedApp.Core/Globals.cs
git commit -m "feat: wire NativePowerPointImportService into Globals startup/shutdown lifecycle"
```

---

### Task 8: Manual integration test

No automated tests are practical here — COM interop requires a real Microsoft Office installation. This task covers manual verification.

**Preconditions:**
- Microsoft PowerPoint is installed on the test machine
- A `.pptx` file is available for testing
- App built in Debug configuration

- [ ] **Step 1: Verify Setup UI**

Run `HandsLiftedApp.Desktop`. Open Setup > Integrations. Confirm:
- "PowerPoint Import" section visible with checkbox
- Checkbox is unchecked by default
- Checking it and closing/reopening Setup retains the value

- [ ] **Step 2: Test Syncfusion path (default)**

With checkbox unchecked:
1. Add a `.pptx` file to a playlist
2. Click Sync on the PowerPoint item
3. Confirm slides appear (PNG thumbnails visible)
4. Check logs: should say "Importing PowerPoint file (Syncfusion):"

- [ ] **Step 3: Test native path**

With checkbox checked (and PowerPoint installed):
1. Add a `.pptx` file to a playlist
2. Click Sync
3. Confirm slides appear
4. Check logs: should say "Importing PowerPoint file (native COM):" and progress lines
5. Verify `HandsLiftedApp.Importer.PowerPointInteropHost.exe` appears briefly in Task Manager then exits

- [ ] **Step 4: Test hash verification**

Replace `HandsLiftedApp.Importer.PowerPointInteropHost.exe` in the output directory with a dummy file (e.g., copy another exe and rename it). Attempt a sync. Confirm: app logs a `SecurityException` and import fails gracefully without crashing.

Restore the real exe after the test.

- [ ] **Step 5: Test crash cleanup**

With native import enabled:
1. Start a sync of a large `.pptx` (so the helper runs for a few seconds)
2. Kill `HandsLiftedApp.Desktop.exe` via Task Manager mid-import
3. Verify `HandsLiftedApp.Importer.PowerPointInteropHost.exe` is also gone from Task Manager (killed by Job Object)

- [ ] **Step 6: Commit test notes**

```
git commit --allow-empty -m "test: manual integration test for native PPTX import (see plan for steps)"
```

---

## Self-Review

**Spec coverage checklist:**

| Requirement | Task |
|-------------|------|
| Feature flag to choose native vs Syncfusion | Tasks 1, 2, 7 |
| Integration from main app to helper process | Tasks 4C, 5 |
| Helper starts as needed (per-import) | Task 5 — launched in `SyncViaNativeInterop()` |
| Helper stops on clean exit | Tasks 5, 7 (Job Object + `Shutdown()`) |
| Helper stops on main app crash | Task 5 (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) |
| Windows-only restriction | Tasks 1 (flag property getter), 5 (`[SupportedOSPlatform]`), 7 (runtime guard) |
| Syncfusion fallback unchanged | Tasks 6 — extracted to `SyncViaSyncfusion()`, not modified |

**No placeholders found.** All steps contain exact file paths, complete code, and exact commands.

**Type consistency:**
- `ImportTask.ExportFileFormatType.PNG` — verified exists in `ImportTask.cs` (`ExportFileFormatType` enum with `PNG` member per investigator output)
- `ImportStats.JobStatusEnum.CompletionSuccess` — matches `ImportStats.cs` enum values
- `NativePowerPointImportService.Initialize(string?)` — matches call in Task 7
- `BuildConstants.HelperExeHash` — generated by MSBuild target in Task 4, namespace `HandsLiftedApp.Core`
- `PowerPointImportSettings.UseNativeInterop` — defined in Task 1, consumed in Tasks 6 and 7
