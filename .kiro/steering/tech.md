# Tech Stack

## Runtime & Language

- .NET 8 (SDK 9.0.x for build tooling, `rollForward: latestMajor` in global.json)
- C# with nullable reference types enabled globally
- Target framework: `net8.0`

## UI Framework

- Avalonia UI 11.3.4 (cross-platform desktop UI)
- Avalonia Fluent theme
- ReactiveUI for MVVM pattern
- Compiled bindings enabled by default (`AvaloniaUseCompiledBindingsByDefault`)
- AXAML for view markup

## Key Libraries

| Purpose | Library |
|---------|---------|
| MVVM / Reactive | ReactiveUI, System.Reactive, DynamicData |
| Logging | Serilog (Console, File, Debug sinks) |
| Serialization | Newtonsoft.Json, YamlDotNet, protobuf-net |
| Configuration | Config.Net |
| Video playback | LibMPV (forked), LibVLCSharp |
| NDI output | AvaloniaNDI (in-repo library) |
| Image processing | Magick.NET, SkiaSharp, AsyncImageLoader.Avalonia |
| PDF rendering | PDFiumCore |
| PowerPoint import | Syncfusion.Presentation.NET, NetOfficeFw.PowerPoint |
| Icons | Material.Icons.Avalonia |
| Testing | MSTest, xunit, Moq, coverlet |

## Build System

- MSBuild via `dotnet` CLI
- Solution file: `HandsLiftedApp.sln`
- Central package management (`Directory.Packages.props`)
- Shared build properties (`Directory.Build.props`)
- Local NuGet package source in `global-packages/` folder

## Common Commands

```shell
# Restore all packages
dotnet restore

# Build the full solution (Release)
dotnet build -c Release

# Build the full solution (Debug)
dotnet build

# Run tests (excludes long-running tests)
dotnet test -c Release --filter "Category!=LongRunning"

# Run the desktop app
dotnet run --project HandsLiftedApp.Desktop
```

## CI/CD

- GitHub Actions on `windows-latest`
- Triggers on PRs to `main`, `dev`, `dev-2024`, `dev-2025`
- Pipeline: restore → build (Release) → test

## Code Style (from .editorconfig)

- Indentation: tabs, size 4
- Line endings: CRLF
- Interfaces prefixed with `I` (PascalCase)
- Types and non-field members: PascalCase
- Block-scoped namespaces (not file-scoped)
- Prefer braces (silent enforcement)
- Null propagation and coalesce expressions preferred

## Environment Variables

- `SyncfusionLicenseKey` — required for PowerPoint import via Syncfusion (free community license available)
