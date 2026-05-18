# Requirements Document

## Introduction

Expose the motion background video file selection in the Song Editor window. Users need a way to assign, change, and clear the motion background video path for a song directly from the Song Editor, without needing to use external tools or edit XML. The UI follows the existing SlideThemeDesigner pattern (TextBox + Browse button) and is placed as a new tab in the left-side TabControl of the SongEditorControl.

## Glossary

- **SongEditorControl**: The Avalonia UserControl that provides the song editing interface, containing a left-side TabControl and a right-side slide preview panel.
- **MotionBackgroundTab**: The new tab added to the SongEditorControl left-side TabControl for managing the motion background video path.
- **FilePicker**: The Avalonia StorageProvider file picker dialog used to browse and select files from the filesystem.
- **MotionBackgroundVideoPath**: The reactive string property on SongItemInstance that stores the absolute path to the motion background video file.
- **SongEditorViewModel**: The ReactiveUI ViewModel backing the SongEditorControl, exposing the Song (SongItemInstance) to the view.

## Requirements

### Requirement 1: Motion Background Tab Placement

**User Story:** As a worship operator, I want a dedicated tab in the Song Editor for motion background settings, so that I can manage the video background without leaving the editor.

#### Acceptance Criteria

1. THE SongEditorControl SHALL display a "Motion Background" tab in the left-side TabControl alongside the existing "Text" and "Stanzas" tabs.
2. WHEN the "Motion Background" tab is selected, THE SongEditorControl SHALL display the motion background file selection controls in the tab content area.
3. THE MotionBackgroundTab SHALL appear after the "Stanzas" tab in the tab order.

### Requirement 2: File Path Display

**User Story:** As a worship operator, I want to see the currently assigned motion background video path, so that I can verify which video is configured for the song.

#### Acceptance Criteria

1. THE MotionBackgroundTab SHALL display a read-only TextBox showing the current value of MotionBackgroundVideoPath.
2. WHEN MotionBackgroundVideoPath is null or empty, THE TextBox SHALL display empty with a watermark placeholder indicating no file is selected.
3. WHEN MotionBackgroundVideoPath changes on the SongItemInstance, THE TextBox SHALL update to reflect the new value.

### Requirement 3: Browse for Video File

**User Story:** As a worship operator, I want to browse for a video file using a file picker dialog, so that I can select a motion background video from my filesystem.

#### Acceptance Criteria

1. THE MotionBackgroundTab SHALL display a Browse button (labeled "...") adjacent to the file path TextBox.
2. WHEN the user clicks the Browse button, THE SongEditorControl SHALL open a FilePicker dialog configured with the title "Select Motion Background Video".
3. THE FilePicker SHALL filter to video files with extensions: .mp4, .mov, .avi, .wmv, .mkv, .webm.
4. THE FilePicker SHALL NOT include an "All Files" fallback filter option.
5. WHEN the user selects a file in the FilePicker, THE SongEditorControl SHALL set MotionBackgroundVideoPath on the SongItemInstance to the selected file's absolute path.
6. WHEN the user cancels the FilePicker dialog, THE SongEditorControl SHALL leave MotionBackgroundVideoPath unchanged.

### Requirement 4: Clear Video Path

**User Story:** As a worship operator, I want to clear the assigned motion background video, so that I can remove the video background from a song.

#### Acceptance Criteria

1. THE MotionBackgroundTab SHALL display a Clear button adjacent to the Browse button.
2. WHEN the user clicks the Clear button, THE SongEditorControl SHALL set MotionBackgroundVideoPath on the SongItemInstance to null.
3. WHILE MotionBackgroundVideoPath is null or empty, THE Clear button SHALL remain enabled (clearing an already-empty path is a no-op with no error).

### Requirement 5: UI Layout Consistency

**User Story:** As a worship operator, I want the motion background controls to look consistent with the rest of the application, so that the interface feels cohesive.

#### Acceptance Criteria

1. THE MotionBackgroundTab SHALL use a horizontal DockPanel layout with the Clear button docked right, the Browse button docked right, and the TextBox filling the remaining space.
2. THE MotionBackgroundTab SHALL follow the same visual pattern as the SlideThemeDesigner background graphic file path control (TextBox + "..." Button).
3. THE MotionBackgroundTab controls SHALL use the same styling and spacing conventions as other tabs in the SongEditorControl.
