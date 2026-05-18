# Project Structure

## Solution Layout

```
HandsLiftedApp.sln
├── HandsLiftedApp.Desktop/        # Desktop entry point (exe), platform bootstrapping
├── HandsLiftedApp.Core/           # Main application: views, view models, controllers, rendering
├── HandsLiftedApp.Controls/       # Reusable Avalonia UI controls
├── HandsLiftedApp.Data/           # Data models, types, documents, serialization
├── HandsLiftedApp.Utils/          # Common utilities (assembly name: HandsLiftedApp.Common)
├── HandsLiftedApp.Tests/          # Unit tests (MSTest + xunit)
│
├── HandsLiftedApp.Importer.PowerPointLib/       # PowerPoint import via Syncfusion
├── HandsLiftedApp.Importer.PowerPointInteropHost/ # PowerPoint import via COM interop
├── HandsLiftedApp.Importer.PDF/                 # PDF slide import
├── HandsLiftedApp.GoogleSlidesImporter/         # Google Slides import
├── HandsLiftedApp.Importer.OnlineSongLyrics/    # Online lyrics fetching
├── HandsLiftedApp.Importer.FileFormatConvertTaskData/ # File format conversion tasks
├── HandsLiftedApp.SongSelectImporter/           # SongSelect integration
│
├── HandsLiftedApp.Auth.GoogleDrive/             # Google Drive authentication
├── HandsLiftedApp.Browser/                      # Embedded browser (Avalonia web target)
├── HandsLiftedApp.ControlSurface.WiiRemote/     # Wii Remote input
├── HandsLiftedApp.PropertyGridControl/          # Property editor control
├── HandsLiftedApp.ThumbnailExtractor/           # Thumbnail generation
├── HandsLiftedApp.XTransitioningContentControl/ # Animated content transitions
│
├── HandsLifted.SlideDesigner/          # Slide designer library
├── HandsLifted.SlideDesigner.Desktop/  # Slide designer standalone app
├── HandsLifted.FetchBible/             # Bible text fetching utility
│
├── Libraries/                     # Forked/vendored third-party libraries
│   ├── AvaloniaNDI/               # NDI output for Avalonia
│   ├── LibMpv/                    # MPV video player bindings
│   ├── vlcsharpavalonia/          # VLC player for Avalonia
│   └── nativevlcsharpavalonia/    # Native VLC integration
│
├── Demos/                         # Standalone demo/sample apps
├── obs-integration/               # OBS streaming integration
└── global-packages/               # Local NuGet package cache
```

## Core Project Internal Structure (HandsLiftedApp.Core)

```
HandsLiftedApp.Core/
├── Assets/              # Icons, images, default themes
├── BuildInfo/           # Git hash and build date (auto-generated)
├── Controller/          # Application controllers
├── Controls/            # App-specific controls
├── Converters/          # XAML value converters
├── JsonConverter/       # JSON serialization converters
├── Models/              # Core domain models
├── Render/              # Slide rendering (song themes, lower thirds, video)
├── Services/            # Application services
├── Utils/               # Internal utilities
├── ViewModels/          # MVVM view models
└── Views/               # AXAML views and windows
    ├── AddItem/         # Add item dialogs
    ├── Confirmation/    # Confirmation dialogs
    ├── ContentControl/  # Media playback controls
    ├── Designer/        # Slide designer views
    ├── Editors/         # Song/item editors
    ├── Items/           # Item-specific views
    ├── Library/         # Song library browser
    ├── Setup/           # Display setup
    └── StageDisplayLayout/ # Stage monitor layouts
```

## Dependency Flow

```
Desktop → Core → Controls → Data → XTransitioningContentControl
                → Importers (PDF, PowerPoint, Google Slides, Lyrics)
                → Libraries (NDI, LibMpv)
```

## Conventions

- Each project lives in its own top-level folder named after the assembly
- Views use `.axaml` + `.axaml.cs` code-behind pattern
- ViewModels follow ReactiveUI conventions (ReactiveObject base)
- Importers are isolated into separate projects per source format
- Forked libraries live under `Libraries/` and are referenced as project references
- Demo/sample apps live under `Demos/`
