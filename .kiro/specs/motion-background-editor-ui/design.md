# Design Document: Motion Background Editor UI

## Architecture Overview

This feature adds a "Motion Background" tab to the existing `SongEditorControl` left-side `TabControl`. The tab exposes a file path TextBox and Browse/Clear buttons, following the same pattern used in `SlideThemeDesigner` for the background graphic file path. No new ViewModel is required — the existing `SongEditorViewModel.Song.MotionBackgroundVideoPath` property provides the data binding target.

The implementation is entirely within the view layer (AXAML + code-behind), leveraging the existing reactive property infrastructure on `SongItemInstance`.

## Components

### Modified Files

| File | Change |
|------|--------|
| `SongEditorControl.axaml` | Add a third `TabItem` ("Motion Background") to the left-side `TabControl` |
| `SongEditorControl.axaml.cs` | Add `BrowseMotionBackground_OnClick` and `ClearMotionBackground_OnClick` event handlers |

### No New Files Required

The feature reuses existing infrastructure:
- `SongItemInstance.MotionBackgroundVideoPath` (reactive property, already exists)
- `SongItemInstance.HasMotionBackground` (computed property, already exists)
- `TopLevel.StorageProvider.OpenFilePickerAsync` (Avalonia file picker, already used in the codebase)

## Detailed Design

### Tab Layout (AXAML)

The new `TabItem` is added after the "Stanzas" tab in the `TabControl`:

```xml
<TabItem Header="Motion Background">
	<DockPanel Margin="8">
		<Button DockPanel.Dock="Right"
		        Click="ClearMotionBackground_OnClick"
		        Content="Clear"
		        Margin="4,0,0,0" />
		<Button DockPanel.Dock="Right"
		        Click="BrowseMotionBackground_OnClick"
		        Content="..."
		        Margin="4,0,0,0" />
		<TextBox IsReadOnly="True"
		         Watermark="No video file selected"
		         Text="{Binding Song.MotionBackgroundVideoPath}" />
	</DockPanel>
</TabItem>
```

Key layout decisions:
- `DockPanel` with buttons docked right, TextBox filling remaining space (matches SlideThemeDesigner pattern)
- Clear button is rightmost, Browse button is between TextBox and Clear
- TextBox is read-only — path is only set via the Browse button or cleared via Clear
- Watermark provides guidance when no file is selected

### Browse Event Handler (Code-Behind)

```csharp
private async void BrowseMotionBackground_OnClick(object? sender, RoutedEventArgs e)
{
	var topLevel = TopLevel.GetTopLevel(this);
	if (topLevel == null) return;

	var videoFileType = new FilePickerFileType("Video Files")
	{
		Patterns = new[] { "*.mp4", "*.mov", "*.avi", "*.wmv", "*.mkv", "*.webm" }
	};

	var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
	{
		Title = "Select Motion Background Video",
		AllowMultiple = false,
		FileTypeFilter = new[] { videoFileType }
	});

	if (files.Count >= 1)
	{
		var path = files[0].TryGetLocalPath();
		if (path != null && this.DataContext is SongEditorViewModel vm)
		{
			vm.Song.MotionBackgroundVideoPath = path;
		}
	}
}
```

### Clear Event Handler (Code-Behind)

```csharp
private void ClearMotionBackground_OnClick(object? sender, RoutedEventArgs e)
{
	if (this.DataContext is SongEditorViewModel vm)
	{
		vm.Song.MotionBackgroundVideoPath = null;
	}
}
```

## Data Flow

```
┌─────────────────────────────────────────────────────────┐
│ SongEditorControl (View)                                │
│                                                         │
│  [TextBox] ←── binding ──── Song.MotionBackgroundVideoPath
│  [... Button] ── click ──→ OpenFilePickerAsync          │
│  [Clear Button] ── click ──→ set path = null            │
└─────────────────────────────────────────────────────────┘
         │                           ▲
         │ sets property             │ RaiseAndSetIfChanged
         ▼                           │
┌─────────────────────────────────────────────────────────┐
│ SongItemInstance (ViewModel/Model)                       │
│                                                         │
│  MotionBackgroundVideoPath : string?                    │
│  HasMotionBackground : bool (computed)                  │
│                                                         │
│  WhenAnyValue(MotionBackgroundVideoPath)                │
│    → RaisePropertyChanged(HasMotionBackground)          │
│    → RegenerateAllSlideBitmaps() if newly valid         │
└─────────────────────────────────────────────────────────┘
```

## Interfaces

No new public interfaces are introduced. The feature uses:

- `SongItemInstance.MotionBackgroundVideoPath` (existing `string?` reactive property)
- `SongEditorViewModel.Song` (existing `SongItemInstance` property)
- `IStorageProvider.OpenFilePickerAsync` (Avalonia platform API)

## Error Handling

| Scenario | Handling |
|----------|----------|
| `TopLevel.GetTopLevel(this)` returns null | Early return, no dialog shown |
| User cancels file picker | `files.Count == 0`, no property change |
| `TryGetLocalPath()` returns null (e.g., sandboxed storage) | No property change, path not set |
| Clear clicked when path is already null | No-op, property setter handles null → null gracefully via `RaiseAndSetIfChanged` |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Reactive binding reflects model changes

*For any* valid file path string assigned to `SongItemInstance.MotionBackgroundVideoPath`, the bound TextBox in the Motion Background tab SHALL display that exact string value.

**Validates: Requirements 2.3**

### Property 2: Browse sets model to selected path

*For any* file path returned by the file picker dialog, clicking the Browse button and selecting that file SHALL result in `SongItemInstance.MotionBackgroundVideoPath` being set to the file's absolute local path.

**Validates: Requirements 3.5**

### Property 3: Clear nullifies the path

*For any* initial value of `MotionBackgroundVideoPath` (including null, empty, or a valid path), clicking the Clear button SHALL result in `MotionBackgroundVideoPath` being set to null.

**Validates: Requirements 4.2**
