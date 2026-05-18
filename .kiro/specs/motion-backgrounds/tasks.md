# Implementation Plan: Motion Backgrounds

## Overview

This plan implements looping video motion backgrounds for Song items, rendered beneath lyrics via a dedicated LibMPV context. The implementation adds a new compositing layer, transparent bitmap rendering, and reactive playback lifecycle management integrated with the existing Avalonia UI / ReactiveUI architecture.

## Tasks

- [x] 1. Data model changes and configuration property
  - [x] 1.1 Add MotionBackgroundVideoPath property to SongItem
    - Add `_motionBackgroundVideoPath` backing field and `MotionBackgroundVideoPath` property with `RaiseAndSetIfChanged` to `HandsLiftedApp.Data/Models/Items/SongItem.cs`
    - Ensure the property is XML-serializable (public getter/setter, string? type)
    - _Requirements: 5.1, 5.2_

  - [x] 1.2 Add HasMotionBackground computed property to SongItemInstance
    - Add `[XmlIgnore] public bool HasMotionBackground` property to `HandsLiftedApp.Core/Models/RuntimeData/Items/SongItemInstance.cs`
    - Property returns true when `MotionBackgroundVideoPath` is non-null, non-empty, and has a valid video extension
    - Wire `PropertyChanged` on `MotionBackgroundVideoPath` to raise `PropertyChanged` for `HasMotionBackground`
    - _Requirements: 5.1, 5.3_

  - [x] 1.3 Add HasMotionBackground property to SongSlideInstance
    - Add `[XmlIgnore] public bool HasMotionBackground` property to `SongSlideInstance` that delegates to the parent `SongItemInstance.HasMotionBackground`
    - Ensure the property is accessible from AXAML bindings in SongSlideView
    - _Requirements: 4.1, 4.2_

  - [x] 1.4 Trigger bitmap regeneration when MotionBackgroundVideoPath changes
    - In `SongItemInstance`, subscribe to `MotionBackgroundVideoPath` property changes
    - When path changes from null/empty to a valid path, emit `SlideRenderRequestMessage` for each `SongSlideInstance` belonging to the item
    - When path changes from valid to null/empty, defer regeneration until next slide transition (do not regenerate immediately)
    - _Requirements: 4.3, 4.4_

- [x] 2. MotionBackgroundService creation
  - [x] 2.1 Create MotionBackgroundService static class
    - Create `HandsLiftedApp.Core/Services/MotionBackgroundService.cs`
    - Implement `CreateMotionMpvContext()` factory method that configures MpvContext with `vo=libmpv`, `force-window=no`, `loop-file=inf`, `video-sync=display-resample`, `aid=no`, `mute=yes`
    - Implement `DisposeContext(ref MpvContext? context)` for safe disposal with error handling
    - Implement `IsValidVideoFile(string? filePath)` validating extensions: .mp4, .mov, .avi, .wmv, .mkv, .webm
    - Implement `ResolveVideoPath(string? relativePath, string? playlistDirectory)` using existing `RelativeFilePathResolver`
    - Log errors via Serilog on initialization failure; never throw unhandled exceptions
    - _Requirements: 3.1, 3.3, 3.4, 3.5, 5.2, 6.4_

  - [ ]* 2.2 Write property test for video path validation (Property 8)
    - **Property 8: Video path validation**
    - Generate random strings including null, empty, valid extensions, invalid extensions
    - Assert `IsValidVideoFile` returns true only for non-null, non-empty paths with supported extensions
    - **Validates: Requirements 5.1, 5.2**

  - [ ]* 2.3 Write property test for path serialization round-trip (Property 9)
    - **Property 9: Path serialization round-trip**
    - Generate random absolute paths and playlist directory paths
    - Assert `ToRelativePath` followed by `ToAbsolutePath` produces equivalent path to original
    - **Validates: Requirements 5.5, 5.6**

- [x] 3. MotionBackgroundLayer UserControl creation
  - [x] 3.1 Create MotionBackgroundLayer AXAML and code-behind
    - Create `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundLayer.axaml` with SoftwareVideoView, UniformToFill stretch, 1920x1080 dimensions, initial Opacity=0, DoubleTransition on Opacity
    - Create `HandsLiftedApp.Core/Render/MotionBackground/MotionBackgroundLayer.axaml.cs` implementing `UserControl, IDisposable`
    - Define `ActiveItemProperty` as Avalonia DirectProperty for binding the active playlist item
    - _Requirements: 1.6, 2.2_

  - [x] 3.2 Implement playback lifecycle in MotionBackgroundLayer
    - Implement `StartPlayback(string videoFilePath)` — creates MpvContext via `MotionBackgroundService.CreateMotionMpvContext()`, issues `loadfile` command
    - Implement `StopPlayback()` — issues stop command, disposes context via `MotionBackgroundService.DisposeContext`
    - Implement `FadeIn()` — sets `Opacity = 1` (animated via DoubleTransition)
    - Implement `FadeOut(Action? onComplete)` — sets `Opacity = 0`, invokes callback after transition completes
    - On first frame decoded callback, trigger `FadeIn()`
    - _Requirements: 2.1, 2.2, 2.5, 3.4_

  - [x] 3.3 Implement reactive item-change detection in MotionBackgroundLayer
    - Observe `ActiveItem` property changes via ReactiveUI
    - When new item is `SongItemInstance` with `HasMotionBackground == true`: if currently playing a different video, fade out → stop → start new → fade in; if no current playback, start new → fade in
    - When new item has no motion background: fade out → stop playback
    - When navigating slides within the same SongItem (ActiveItem unchanged), do NOT restart playback
    - _Requirements: 1.7, 2.4, 2.6, 2.7_

  - [x] 3.4 Implement error handling in MotionBackgroundLayer
    - Wrap all MpvContext commands in try/catch for `MpvException`
    - On file-not-found: log warning with path, render transparent
    - On decode failure: log error with path and reason, stop playback, render transparent
    - On file-unreadable during playback: stop playback, render transparent, log warning
    - Never display error dialogs or throw unhandled exceptions
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ]* 3.5 Write property test for slide navigation continuity (Property 3)
    - **Property 3: Slide navigation does not restart playback**
    - Generate random SongItemInstance with motion background and random slide index sequences within bounds
    - Assert no `loadfile` or `stop` commands are issued to Motion_MpvContext during slide navigation
    - **Validates: Requirements 2.3**

  - [ ]* 3.6 Write property test for motion background transition on item change (Property 2)
    - **Property 2: Motion background transition on item change**
    - Generate pairs of SongItemInstances both with valid MotionBackgroundVideoPath
    - Assert fade out → stop → loadfile (new path) → fade in sequence occurs
    - **Validates: Requirements 1.7, 2.6**

  - [ ]* 3.7 Write property test for fade out on any item change (Property 4)
    - **Property 4: Previous video fades out on any item change**
    - Generate SongItemInstance with motion background followed by any different item
    - Assert Opacity animates 1→0 and stop command is issued
    - **Validates: Requirements 2.5, 2.7**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Transparent bitmap rendering modifications
  - [x] 5.1 Modify SongSlideView for conditional transparent background
    - In `HandsLiftedApp.Core/Views/Designer/SongSlideView.axaml`, add conditional binding on the Grid background and Border ImageBrush
    - When `HasMotionBackground` is true: set Background to `Transparent`, remove/null the ImageBrush
    - When `HasMotionBackground` is false: retain existing theme colour + background image behaviour
    - Ensure pixel format supports alpha channel for transparent rendering
    - _Requirements: 4.1, 4.2, 4.5_

  - [ ]* 5.2 Write property test for bitmap background determined by HasMotionBackground (Property 6)
    - **Property 6: Bitmap background determined by HasMotionBackground**
    - Generate random SongSlideInstances with varying HasMotionBackground states, theme colours, and text content
    - Assert transparent background (alpha=0) when HasMotionBackground is true, theme colour when false
    - **Validates: Requirements 4.1, 4.2**

  - [ ]* 5.3 Write property test for transparency compositing correctness (Property 1)
    - **Property 1: Transparency compositing correctness**
    - Generate song slide bitmaps with HasMotionBackground=true, random text, random themes
    - Assert background region pixels have alpha=0
    - **Validates: Requirements 1.3, 4.1**

  - [ ]* 5.4 Write property test for bitmap regeneration count (Property 7)
    - **Property 7: Bitmap regeneration on motion background addition**
    - Generate SongItemInstances with 1-20 slides
    - Set MotionBackgroundVideoPath from null to valid path
    - Assert exactly N SlideRenderRequestMessages emitted (one per slide)
    - **Validates: Requirements 4.4**

- [x] 6. ProjectorWindow and StageDisplay AXAML modifications
  - [x] 6.1 Insert MotionBackgroundLayer into ProjectorWindow
    - In `ProjectorWindow.axaml`, add `<motionBg:MotionBackgroundLayer>` inside the NDISendContainer Grid, before `ActiveSlideRender` (lower Z-index)
    - Bind `ActiveItem="{Binding Playlist.SelectedItem}"`
    - Verify compositing order: MotionBackgroundLayer → ActiveSlideRender → VideoLayerRenderer
    - Ensure MotionBackgroundLayer is inside NDI main output container but NOT in the lyrics-only NDI output
    - _Requirements: 1.1, 1.5, 7.1, 7.2_

  - [x] 6.2 Insert MotionBackgroundLayer into StageDisplay DefaultLayout
    - In `DefaultLayout.axaml` (Stage Display), add `<motionBg:MotionBackgroundLayer>` before `SlideRender` in the Live preview Grid
    - Bind `ActiveItem` to the same playlist selected item source
    - Stage Display uses its own independent Motion_MpvContext instance (each MotionBackgroundLayer creates its own)
    - _Requirements: 1.2, 7.3, 7.4_

  - [ ]* 6.3 Write property test for MpvContext independence (Property 5)
    - **Property 5: MpvContext independence**
    - Generate random MPV command sequences issued to Motion_MpvContext
    - Assert `Globals.Instance.MpvContextInstance` properties remain unchanged, and vice versa
    - **Validates: Requirements 3.2**

- [x] 7. Serialization changes
  - [x] 7.1 Update HandsLiftedDocXmlSerializer for MotionBackgroundVideoPath
    - In `SerializeItem` for SongItemInstance, convert `MotionBackgroundVideoPath` to relative path via `RelativeFilePathResolver.ToRelativePath` before writing XML
    - In deserialization, resolve the stored relative path back to absolute via `RelativeFilePathResolver.ToAbsolutePath`
    - Handle null/empty path gracefully (do not write element if null)
    - _Requirements: 5.5, 5.6_

- [x] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Unit tests
  - [ ]* 9.1 Write unit tests for MotionBackgroundLayer visibility and compositing
    - Test: layer is invisible (Opacity=0) when ActiveItem has no motion background
    - Test: Grid compositing order is MotionBG → ActiveSlide → VideoLayer
    - Test: SoftwareVideoView uses UniformToFill stretch
    - _Requirements: 1.4, 1.1, 1.2, 1.5, 1.6_

  - [ ]* 9.2 Write unit tests for MpvContext configuration and lifecycle
    - Test: MpvContext created by service is not the global singleton
    - Test: MpvContext configured with vo=libmpv, force-window=no, loop-file=inf
    - Test: MpvContext disposed on deactivation
    - Test: MpvContext init failure logs and continues without crash
    - _Requirements: 3.1, 3.3, 3.4, 3.5_

  - [ ]* 9.3 Write unit tests for HasMotionBackground and path validation
    - Test: null/empty path → HasMotionBackground is false
    - Test: valid path with supported extension → HasMotionBackground is true
    - Test: invalid path falls back to standard rendering
    - Test: pixel format supports alpha channel
    - _Requirements: 5.3, 5.4, 4.5_

  - [ ]* 9.4 Write unit tests for error handling and logging
    - Test: missing file logs warning with path
    - Test: undecodable file logs error with path and reason
    - Test: file unreadable during playback → graceful stop
    - Test: deferred regeneration on config removal (no immediate re-render)
    - _Requirements: 6.1, 6.2, 6.3, 4.3_

  - [ ]* 9.5 Write unit tests for NDI and Stage Display output
    - Test: NDI lyrics-only output does not include motion background layer
    - Test: Stage Display uses independent MpvContext instance
    - _Requirements: 7.2, 7.4_

- [ ] 10. Error resilience property test
  - [ ]* 10.1 Write property test for error resilience (Property 10)
    - **Property 10: Error resilience — no unhandled exceptions**
    - Generate random invalid paths: null, empty, >260 chars, unsupported extensions, non-existent files
    - Assert activating a SongItem with invalid path does not throw, does not show error dialog, renders transparent
    - **Validates: Requirements 6.4**

- [ ] 11. Integration tests
  - [ ]* 11.1 Write integration tests for compositing and output
    - Test: NDI main output frame contains motion background pixels when active
    - Test: Stage Display renders motion background beneath content
    - Test: Opacity animates 0→1 on activation (fade in)
    - Test: Opacity animates 1→0 on deactivation (fade out)
    - Test: Video loops seamlessly (no black frames between loops)
    - _Requirements: 7.1, 7.3, 2.2, 2.5, 2.3_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck (FsCheck.Xunit)
- Unit tests validate specific examples and edge cases using xunit
- The MotionBackgroundLayer creates its own MpvContext per instance, ensuring Projector and Stage Display are independent
- Transparent bitmap rendering is controlled by binding to `HasMotionBackground` — no changes to `BaseSlideTheme` are needed
- All error handling follows the "never interrupt the presentation" principle — errors are logged, layer degrades to transparent

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.2", "2.3"] },
    { "id": 2, "tasks": ["1.4", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "3.4"] },
    { "id": 4, "tasks": ["3.5", "3.6", "3.7", "5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "5.4", "6.1", "6.2"] },
    { "id": 6, "tasks": ["6.3", "7.1"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "10.1"] },
    { "id": 8, "tasks": ["11.1"] }
  ]
}
```
