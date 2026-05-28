# Slide Transition Duration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the slide cross-fade duration a configurable per-playlist value, settable via a Slider in the LivePane UI on MainWindow.

**Architecture:** Add `SlideTransitionDurationMs` (double, default 120) to the `Playlist` data model so it serializes/deserializes with the playlist file. Propagate the value through the serializer and load mapping. Replace the two hardcoded `TimeSpan.FromMilliseconds(120)` call sites with the live property. Add a compact Slider in `LivePane.axaml` bound directly to `Playlist.SlideTransitionDurationMs`.

**Tech Stack:** C#, ReactiveUI, Avalonia 11 AXAML, XmlSerializer (System.Xml.Serialization)

---

## File Structure

| File | Change |
|---|---|
| `HandsLiftedApp.Data/Models/Playlist.cs` | Add `SlideTransitionDurationMs` property (double, default 120) |
| `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs` | Copy property in `SerializePlaylist` |
| `HandsLiftedApp.Core/ViewModels/MainViewModel.cs` | Copy property in playlist load mapping |
| `HandsLiftedApp.Core/Views/LivePane.axaml` | Add Slider + label row |
| `HandsLiftedApp.Core/Views/LivePane.axaml.cs` | Read `_vm.Playlist.SlideTransitionDurationMs` |
| `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs` | Same |

---

## Task 1: Add `SlideTransitionDurationMs` to the Playlist data model

**Files:**
- Modify: `HandsLiftedApp.Data/Models/Playlist.cs`

Background: `Playlist` is the XmlSerializer data model. `PlaylistInstance` inherits from it. Adding the property here means both the serialized model and the runtime instance get it automatically. Using `double` matches Avalonia's `Slider.Value` type and `TimeSpan.FromMilliseconds(double)`, avoiding any binding converters.

- [ ] **Step 1: Open `HandsLiftedApp.Data/Models/Playlist.cs`**

The file currently ends after the `Items` property. Add the new property after `Items`:

```csharp
private double _slideTransitionDurationMs = 120.0;
public double SlideTransitionDurationMs
{
    get => _slideTransitionDurationMs;
    set => this.RaiseAndSetIfChanged(ref _slideTransitionDurationMs, value);
}
```

Place this block after the closing brace of the `Items` property block and before the closing `}` of the class.

- [ ] **Step 2: Build to confirm no errors**

Run: `dotnet build HandsLiftedApp.Data/HandsLiftedApp.Data.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Data/Models/Playlist.cs
git commit -m "feat: add SlideTransitionDurationMs property to Playlist model"
```

---

## Task 2: Propagate through the serializer

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`

Background: `SerializePlaylist` manually builds a plain `Playlist` object from a `PlaylistInstance` and serializes it. Only explicitly listed properties are included — adding `SlideTransitionDurationMs` to the list is required for it to round-trip. XmlSerializer handles the `double` type natively; no `[XmlElement]` attribute is needed.

- [ ] **Step 1: Open `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs` and find `SerializePlaylist`**

Locate the object initializer that looks like:
```csharp
Playlist playlistSerialized = new Playlist
{
    Title = playlist.Title,
    Meta = playlist.Meta,
    LogoGraphicFile = RelativeFilePathResolver.ToRelativePath(...),
    Designs = ...,
    Items = new TrulyObservableCollection<Item>()
};
```

Add `SlideTransitionDurationMs` to the initializer:

```csharp
Playlist playlistSerialized = new Playlist
{
    Title = playlist.Title,
    Meta = playlist.Meta,
    LogoGraphicFile =
        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath, playlist.LogoGraphicFile),
    SlideTransitionDurationMs = playlist.SlideTransitionDurationMs,
    Designs = new ObservableCollection<BaseSlideTheme>(playlist.Designs.Select(design =>
    {
        if (design.BackgroundGraphicFilePath != null)
        {
            design.BackgroundGraphicFilePath =
                RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                    design.BackgroundGraphicFilePath);
        }
        return design;
    }).ToList()),
    Items = new TrulyObservableCollection<Item>()
};
```

- [ ] **Step 2: Build to confirm no errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs
git commit -m "feat: serialize SlideTransitionDurationMs in playlist XML"
```

---

## Task 3: Copy property in the playlist load mapping

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`

Background: When a playlist file is opened, `DeserializePlaylist()` returns a plain `Playlist` object (`x`). MainViewModel manually copies specific properties from `x` onto the existing `PlaylistInstance`. The copy list currently includes `Title`, `Meta`, `LogoGraphicFile`, `Designs`, `Items` — but not `SlideTransitionDurationMs`. Without this step, opening a saved playlist would reset the duration to the default 120ms.

- [ ] **Step 1: Locate the property mapping block in `MainViewModel.cs`**

Find the block that looks like (around line 168):
```csharp
Playlist.Title = x.Title;
Playlist.Meta = x.Meta;
Playlist.LogoGraphicFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath, x.LogoGraphicFile);
```

Add one line after `Playlist.Title = x.Title;`:

```csharp
Playlist.SlideTransitionDurationMs = x.SlideTransitionDurationMs;
```

- [ ] **Step 2: Build to confirm no errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "feat: restore SlideTransitionDurationMs when loading playlist from file"
```

---

## Task 4: Use the property at the transition call sites

**Files:**
- Modify: `HandsLiftedApp.Core/Views/LivePane.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs`

Background: Both files already hold a `_vm` field (type `MainViewModel`). The transition duration is currently hardcoded to `120`. Replace the literal with `_vm.Playlist.SlideTransitionDurationMs`.

- [ ] **Step 1: Update `LivePane.axaml.cs`**

Find (around line 81):
```csharp
LivePreviewCanvas.Transition(spec, TimeSpan.FromMilliseconds(120));
```

Replace with:
```csharp
LivePreviewCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.SlideTransitionDurationMs ?? 120));
```

- [ ] **Step 2: Update `ProjectorWindow.axaml.cs`**

Find (around line 122):
```csharp
MainSlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(120));
```

Replace with:
```csharp
MainSlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.SlideTransitionDurationMs ?? 120));
```

- [ ] **Step 3: Build to confirm no errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/LivePane.axaml.cs HandsLiftedApp.Core/Views/ProjectorWindow.axaml.cs
git commit -m "feat: use playlist SlideTransitionDurationMs at transition call sites"
```

---

## Task 5: Add the Slider UI in LivePane

**Files:**
- Modify: `HandsLiftedApp.Core/Views/LivePane.axaml`

Background: `LivePane.axaml` has `x:DataType="viewModels:MainViewModel"` and uses a `DockPanel` layout. The existing controls area has `DockPanel.Dock="Top"` rows for navigation (prev/next/count) and Logo/Blank radio buttons. The new row goes immediately after the Logo/Blank border. `Slider.Value` is `double` and binds directly to `SlideTransitionDurationMs` (also `double`) — no converter needed.

- [ ] **Step 1: Open `HandsLiftedApp.Core/Views/LivePane.axaml`**

Locate the Logo/Blank radio button border. It looks like:
```xml
<Border
    Height="35"
    Margin="6,6"
    ...
    DockPanel.Dock="Top">
    <UniformGrid Columns="2" Rows="1">
        ...
        <RadioButton CornerRadius="6" IsChecked="{Binding Playlist.IsLogo}">Logo</RadioButton>
        <RadioButton CornerRadius="6" IsChecked="{Binding Playlist.IsBlank}">Blank</RadioButton>
    </UniformGrid>
</Border>
```

Insert the following block immediately after the closing `</Border>` of that Logo/Blank row:

```xml
<Border
    Height="35"
    Margin="6,0,6,6"
    Background="Transparent"
    MaxWidth="300"
    DockPanel.Dock="Top">
    <DockPanel>
        <TextBlock
            VerticalAlignment="Center"
            Margin="8,0,6,0"
            FontSize="13"
            Text="Fade:" />
        <TextBlock
            VerticalAlignment="Center"
            DockPanel.Dock="Right"
            Margin="4,0,8,0"
            FontSize="13"
            Text="{Binding Playlist.SlideTransitionDurationMs, StringFormat='{}{0:0}ms'}" />
        <Slider
            VerticalAlignment="Center"
            Minimum="0"
            Maximum="2000"
            TickFrequency="100"
            SmallChange="50"
            LargeChange="200"
            Value="{Binding Playlist.SlideTransitionDurationMs}" />
    </DockPanel>
</Border>
```

- [ ] **Step 2: Build to confirm no AXAML errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Manual smoke test**

Launch the app. In the LivePane (right panel of MainWindow):
- Confirm a "Fade:" label and slider appear below the Logo/Blank buttons.
- Confirm the value readout (e.g. "120ms") updates as you drag the slider.
- Navigate between image slides — confirm the fade duration visually matches the slider value.
- Set duration to 0 — slides should snap with no animation.
- Set duration to 2000 — slides should take ~2 seconds to cross-fade.
- Save the playlist, close and reopen — confirm the slider restores the saved value (not reset to 120ms).

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/LivePane.axaml
git commit -m "feat: add slide transition duration slider to LivePane UI"
```

---

## Self-Review

**Spec coverage:**
- ✅ Value stored at playlist level (`Playlist.SlideTransitionDurationMs`)
- ✅ Serialized to/from playlist XML file
- ✅ Restored when playlist loaded from disk
- ✅ Used at both transition call sites (LivePane + ProjectorWindow)
- ✅ UI control on MainWindow (LivePane is part of MainWindow right panel)

**Placeholder scan:** None found.

**Type consistency:** `double SlideTransitionDurationMs` used consistently across all 6 files. `Slider.Value` is `double` — direct binding, no converter.
