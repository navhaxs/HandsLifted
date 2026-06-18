# Third-Party Notices — Design Spec

**Date:** 2026-06-19
**Scope:** Add an Open Source Notices window accessible from the About window in VisionScreens.

---

## Goal

Surface copyright attributions for all significant third-party libraries and assets used in VisionScreens — both for legal compliance (GPL, LGPL, CC BY-SA, proprietary sub-components) and community transparency (MIT/Apache libs).

---

## Trigger

Add a link-style `Button` labelled **"Open Source Notices"** to the footer `StackPanel` in `AboutWindow.axaml`, alongside the existing "View diagnostic logs" and "Generate support info" buttons. Clicking opens `ThirdPartyNoticesWindow` as a modal child of `AboutWindow`.

---

## ThirdPartyNoticesWindow

### Window properties

| Property | Value |
|---|---|
| Size | 700 × 500 |
| CanResize | True |
| WindowStartupLocation | CenterOwner |
| ShowInTaskbar | False |
| Title | "Open Source Notices" |

### Layout

`DockPanel`:

1. **Header** (`DockPanel.Dock="Top"`, `Margin="16,16,16,8"`):
   - `TextBlock` — "Open Source Notices" (18px bold)
   - `TextBlock` — "VisionScreens uses the following third-party software and assets." (13px, muted)

2. **Footer** (`DockPanel.Dock="Bottom"`, `Margin="0,0,12,12"`):
   - `Button` — "Close", right-aligned, `IsCancel="True"`, `IsDefault="True"`, width 120

3. **Body** (fills remaining space, `Margin="16,0,16,8"`):
   - `ScrollViewer` (vertical scroll)
   - → `ItemsControl` bound to `ThirdPartyNotices.All`
   - → `ItemTemplate`: each row is a `Grid` with three columns

### Row layout

```
| Library (bold, ~220px) | License tag (~110px) | Copyright (remaining width) |
```

- Library name: bold, 13px. Has `ToolTip` showing the project URL (if available).
- License tag: smaller text (11px), colored by category:
  - Copyleft (GPL, LGPL, CC BY-SA): amber/orange foreground
  - Permissive (MIT, Apache, BSD, Ms-PL): muted/secondary foreground
  - Proprietary/Commercial: red foreground
- Copyright: 13px, normal weight, muted foreground.

Rows have alternating background for readability. Use `DataGrid` (read-only, no selection highlight needed) — handles column sizing and zebra striping with less custom XAML than `ItemsControl`.

---

## Data model

New file: `HandsLiftedApp.Core/Data/ThirdPartyNotices.cs`

```csharp
namespace HandsLiftedApp.Core.Data;

public record NoticeEntry(
    string Library,
    string License,
    string Copyright,
    string? Url = null);

public static class ThirdPartyNotices
{
    public static readonly IReadOnlyList<NoticeEntry> All =
        new NoticeEntry[]
        {
            new("AsyncImageLoader.Avalonia", "MIT",        "© AsyncImageLoader contributors"),
            new("Avalonia",                 "MIT",        "© Avalonia contributors",             "https://avaloniaui.net"),
            new("AvaloniaNDI",              "Public Domain + Ms-PL", "© AvaloniaNDI contributors"),
            new("BmpSharp",                 "MIT",        "© BmpSharp contributors"),
            new("ByteSize",                 "MIT",        "© Omar Khudeira"),
            new("Config.Net",               "MIT",        "© Ivan Gavryliuk"),
            new("DebounceThrottle",         "MIT",        "© DebounceThrottle contributors"),
            new("DynamicData",              "MIT",        "© Roland Pheasant",                   "https://github.com/reactivemarbles/DynamicData"),
            new("Google APIs (Drive/Slides)","Apache 2.0","© Google LLC",                        "https://github.com/googleapis/google-api-dotnet-client"),
            new("HidApi.Net",               "MIT",        "© HidApi.Net contributors"),
            new("libmpv",                   "GPL 2.0+",   "© mpv contributors",                  "https://mpv.io"),
            new("LibVLC / VideoLAN",        "LGPL 2.1+",  "© VideoLAN",                          "https://videolan.org"),
            new("LoadingIndicators.Avalonia","MIT",        "© LoadingIndicators contributors"),
            new("Magick.NET",               "Apache 2.0", "© Dirk Lemstra",                     "https://github.com/dlemstra/Magick.NET"),
            new("Material.Icons.Avalonia",  "MIT",        "© Material.Icons.Avalonia contributors"),
            new("NAudio",                   "Ms-PL",      "© Mark Heath",                        "https://github.com/naudio/NAudio"),
            new("NaturalSort.Extension",    "MIT",        "© NaturalSort contributors"),
            new("NDI SDK",                  "Proprietary","© Vizrt Group AS — includes RapidJSON (MIT), Speex (BSD), RapidXML (MIT), Opus (BSD), ASIO (Boost 1.0)"),
            new("Newtonsoft.Json",          "MIT",        "© James Newton-King",                 "https://www.newtonsoft.com/json"),
            new("OpenMoji",                 "CC BY-SA 4.0","© OpenMoji contributors",            "https://openmoji.org"),
            new("PDFiumCore",               "BSD-3-Clause","© PDFium Authors"),
            new("protobuf-net",             "Apache 2.0", "© Marc Gravell"),
            new("ReactiveUI",               "MIT",        "© ReactiveUI contributors",           "https://reactiveui.net"),
            new("RtfDomParser",             "MIT",        "© RtfDomParser contributors"),
            new("Serilog",                  "Apache 2.0", "© Serilog contributors",              "https://serilog.net"),
            new("SIL.Scripture",            "MIT",        "© SIL International"),
            new("SkiaSharp",                "MIT",        "© Microsoft",                         "https://github.com/mono/SkiaSharp"),
            new("Syncfusion",               "Commercial", "© Syncfusion Inc.",                   "https://syncfusion.com"),
            new("System.Reactive",          "MIT",        "© .NET Foundation"),
            new("YamlDotNet",               "MIT",        "© Antoine Aubry",                     "https://github.com/aaubry/YamlDotNet"),
        }
        .OrderBy(e => e.Library, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
```

---

## Files to create / modify

| Action | File |
|---|---|
| Modify | `HandsLiftedApp.Core/Views/AboutWindow.axaml` — add "Open Source Notices" button to footer |
| Modify | `HandsLiftedApp.Core/Views/AboutWindow.axaml.cs` — wire button click → open window |
| **Create** | `HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml` |
| **Create** | `HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml.cs` |
| **Create** | `HandsLiftedApp.Core/Data/ThirdPartyNotices.cs` |

---

## Out of scope

- Full license text (summary + URL sufficient)
- Expandable rows
- Search/filter
- Grouping by license type
- Hyperlinking URLs (tooltip on library name is enough)
