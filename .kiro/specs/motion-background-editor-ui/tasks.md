# Implementation Plan: Motion Background Editor UI

## Overview

Add a "Motion Background" tab to the SongEditorControl left-side TabControl, exposing a read-only TextBox for the video path, a Browse ("...") button to open a file picker, and a Clear button to remove the path. Implementation is entirely within the existing view layer — no new files or ViewModels required.

## Tasks

- [ ] 1. Add Motion Background tab to SongEditorControl AXAML
  - [ ] 1.1 Add the "Motion Background" TabItem to the left-side TabControl
    - Insert a new `TabItem` with `Header="Motion Background"` after the "Stanzas" tab
    - Inside the TabItem, add a `DockPanel` with `Margin="8"`
    - Dock a "Clear" `Button` to the right with `Content="Clear"` and `Margin="4,0,0,0"`
    - Dock a "..." `Button` to the right with `Click="BrowseMotionBackground_OnClick"` and `Margin="4,0,0,0"`
    - Add a read-only `TextBox` filling remaining space with `Watermark="No video file selected"` and `Text="{Binding Song.MotionBackgroundVideoPath}"`
    - Wire the Clear button's `Click` to `ClearMotionBackground_OnClick`
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2, 5.3_

- [ ] 2. Implement event handlers in SongEditorControl code-behind
  - [ ] 2.1 Add BrowseMotionBackground_OnClick event handler
    - Add `using Avalonia.Platform.Storage` if not already present
    - Implement `private async void BrowseMotionBackground_OnClick(object? sender, RoutedEventArgs e)`
    - Get `TopLevel` via `TopLevel.GetTopLevel(this)`, early-return if null
    - Create a `FilePickerFileType("Video Files")` with patterns: `*.mp4`, `*.mov`, `*.avi`, `*.wmv`, `*.mkv`, `*.webm`
    - Call `topLevel.StorageProvider.OpenFilePickerAsync` with title "Select Motion Background Video", `AllowMultiple = false`, and the video file type filter (no "All Files" fallback)
    - If a file is selected, call `TryGetLocalPath()` and set `vm.Song.MotionBackgroundVideoPath = path` when path is non-null
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ] 2.2 Add ClearMotionBackground_OnClick event handler
    - Implement `private void ClearMotionBackground_OnClick(object? sender, RoutedEventArgs e)`
    - Cast `DataContext` to `SongEditorViewModel`, set `vm.Song.MotionBackgroundVideoPath = null`
    - _Requirements: 4.1, 4.2, 4.3_

- [ ] 3. Checkpoint
  - Ensure the solution builds cleanly with `dotnet build`, ask the user if questions arise.

- [ ] 4. Verify reactive binding and data flow
  - [ ] 4.1 Confirm TextBox binding updates reactively
    - Verify that `SongItemInstance.MotionBackgroundVideoPath` is a reactive property (uses `RaiseAndSetIfChanged`)
    - If the property does not raise change notifications, add `RaiseAndSetIfChanged` to its setter
    - Confirm the TextBox `Text="{Binding Song.MotionBackgroundVideoPath}"` reflects model changes without manual refresh
    - _Requirements: 2.3_

  - [ ]* 4.2 Write unit tests for Browse and Clear logic
    - Test that calling the Clear handler sets `MotionBackgroundVideoPath` to null
    - Test that setting `MotionBackgroundVideoPath` to a string value is reflected on the property
    - Test that clearing an already-null path does not throw
    - _Requirements: 3.5, 3.6, 4.2, 4.3_

- [ ] 5. Final checkpoint
  - Ensure all tests pass with `dotnet test`, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirement acceptance criteria for traceability
- No new files are created — all changes are in `SongEditorControl.axaml` and `SongEditorControl.axaml.cs`
- The design reuses the existing `SongItemInstance.MotionBackgroundVideoPath` reactive property
- The UI pattern mirrors `SlideThemeDesigner` (TextBox + "..." Button + Clear Button in a DockPanel)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["4.1"] },
    { "id": 3, "tasks": ["4.2"] }
  ]
}
```
