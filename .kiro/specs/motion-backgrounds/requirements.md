# Requirements Document

## Introduction

Motion Backgrounds adds support for continuously looping video backgrounds that render beneath the text layer on Song slides. A motion background persists across all slides within a single Song item, providing a visually dynamic backdrop while lyrics are displayed. The feature requires a dedicated video playback pipeline (separate from the existing video slide player), transparent bitmap rendering for song slides, and a configuration model that associates a motion background video with a Song item's design theme.

## Glossary

- **Motion_Background_Layer**: A video rendering layer positioned beneath the content layer in the projector output compositing stack, responsible for displaying looping video backgrounds.
- **Motion_Background_Video**: A video file configured to loop seamlessly and play continuously behind song lyrics for the duration of a Song item.
- **Content_Layer**: The existing ActiveSlideRender layer that displays pre-rendered song slide bitmaps, images, and custom slides via the XTransitioningContentControl.
- **Song_Item**: A playlist item representing a song, containing one or more SongSlideInstance objects that share a common theme and motion background configuration.
- **Slide_Theme**: A BaseSlideTheme instance that defines visual properties (font, colours, background) for song slide rendering.
- **MpvContext**: A LibMPV playback context instance that manages video decoding and software rendering to a WriteableBitmap.
- **Motion_MpvContext**: A dedicated MpvContext instance used exclusively for motion background video playback, separate from the existing singleton used for video slides.
- **Projector_Window**: The output display window that composites all visual layers and sends the result via NDI.
- **Stage_Display_Window**: A secondary output window for confidence monitors (presenter TVs), which independently composites the same visual layers using its own rendering instances.
- **Bitmap_Renderer**: The off-screen rendering pipeline that generates cached song slide bitmaps from theme and text data via SlideRenderRequestMessage.

## Requirements

### Requirement 1: Motion Background Layer Compositing

**User Story:** As a worship AV operator, I want motion background videos to appear behind the song lyrics, so that the text remains readable over a dynamic visual backdrop.

#### Acceptance Criteria

1. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Projector_Window SHALL render layers in the following compositing order from back to front: Motion_Background_Layer, Content_Layer, VideoLayerRenderer.
2. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Stage_Display_Window SHALL render layers in the following compositing order from back to front: Motion_Background_Layer, Content_Layer, VideoLayerRenderer.
3. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Motion_Background_Layer SHALL be visible through any region of the Content_Layer bitmap where the alpha channel value is less than fully opaque.
4. WHEN a Song_Item without a configured Motion_Background_Video becomes active, THE Motion_Background_Layer SHALL render as fully transparent (showing the window background).
5. THE Projector_Window SHALL maintain the existing VideoLayerRenderer above the Content_Layer for video slide playback.
6. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Motion_Background_Layer SHALL scale the video to fill the entire output area (1920x1080) using uniform-fill scaling, cropping any excess to avoid letterboxing.
7. WHEN a Song_Item with a configured Motion_Background_Video becomes active while a previous Song_Item with a different Motion_Background_Video was active, THE Motion_Background_Layer SHALL fade out the previous video, then fade in the new Motion_Background_Video.

### Requirement 2: Motion Background Video Playback

**User Story:** As a worship AV operator, I want motion background videos to loop seamlessly and persist across slide transitions, so that the visual experience is smooth throughout the entire song.

#### Acceptance Criteria

1. WHEN a Song_Item with a configured Motion_Background_Video becomes the active item, THE Motion_Background_Layer SHALL begin playback of the Motion_Background_Video as soon as the video is decoded and ready, without imposing an artificial delay.
2. WHEN the Motion_Background_Layer begins playback, THE Motion_Background_Layer SHALL fade in from fully transparent to fully opaque over a configurable duration (default 500 milliseconds).
3. WHILE a Song_Item is active, THE Motion_Background_Layer SHALL loop the Motion_Background_Video continuously, returning to the first frame immediately after the last frame with no black frames or pause between loop iterations.
4. WHILE a Song_Item is active and the operator navigates between slides within the same Song_Item, THE Motion_Background_Layer SHALL continue uninterrupted playback without restarting.
5. WHEN a Song_Item is deactivated (another item becomes active), THE Motion_Background_Layer SHALL fade out from fully opaque to fully transparent over a configurable duration (default 500 milliseconds), then stop playback and release the video resource.
6. WHEN a Song_Item with a configured Motion_Background_Video becomes active immediately after another Song_Item that also had a Motion_Background_Video, THE Motion_Background_Layer SHALL fade out the previous video, then begin playback of the new video with a fade in, without requiring the operator to manually clear the previous background.
7. WHEN any new Song_Item becomes active after a Song_Item that had a Motion_Background_Video, THE Motion_Background_Layer SHALL fade out and stop the previous video regardless of whether the new Song_Item has its own Motion_Background_Video configured.

### Requirement 3: Dedicated Motion Background MpvContext

**User Story:** As a developer, I want motion background video playback to use its own MpvContext instance, so that it does not conflict with the existing video slide playback singleton.

#### Acceptance Criteria

1. THE Motion_Background_Layer SHALL use a Motion_MpvContext instance that is separate from the existing Globals.Instance.MpvContextInstance singleton.
2. WHILE both a Motion_Background_Video and a VideoSlideInstance are active simultaneously, THE Motion_MpvContext SHALL maintain independent playback state such that commands issued to one context (play, pause, stop, seek) do not alter the playback position, pause state, or loaded file of the other context.
3. WHEN the Motion_MpvContext is created, THE Motion_Background_Layer SHALL configure it with vo set to libmpv, force-window set to no, and loop playback enabled via the loop-file property set to inf.
4. WHEN the motion background is deactivated or the application shuts down, THE Motion_Background_Layer SHALL stop playback and dispose of the Motion_MpvContext, releasing the underlying mpv_handle. Disposal SHALL also be permitted outside of deactivation or shutdown conditions when explicitly triggered by the application.
5. IF the Motion_MpvContext fails to initialize, THEN THE Motion_Background_Layer SHALL log the failure and continue application operation without motion background video capability.

### Requirement 4: Transparent Song Slide Bitmap Rendering

**User Story:** As a worship AV operator, I want song slide backgrounds to be transparent when a motion background is configured, so that the video shows through behind the lyrics text.

#### Acceptance Criteria

1. WHEN a Song_Item has a configured Motion_Background_Video, THE Bitmap_Renderer SHALL generate song slide bitmaps with a fully transparent background (alpha = 0 for all background pixels) instead of applying the Slide_Theme BackgroundColour or BackgroundGraphicFilePath.
2. WHEN a Song_Item does not have a configured Motion_Background_Video, THE Bitmap_Renderer SHALL generate song slide bitmaps using the Slide_Theme BackgroundColour and BackgroundGraphicFilePath as the background.
3. WHEN the Motion_Background_Video configuration is removed or changed on an active Song_Item, THE Bitmap_Renderer SHALL defer bitmap regeneration until the next slide transition rather than regenerating immediately, so that live output is not disrupted mid-slide during a worship service.
4. WHEN the Motion_Background_Video configuration is added on an active Song_Item, THE Bitmap_Renderer SHALL regenerate all SongSlideInstance bitmaps belonging to that Song_Item. THE Bitmap_Renderer SHALL continue processing to completion regardless of how long regeneration takes.
5. WHEN generating transparent song slide bitmaps, THE Bitmap_Renderer SHALL use a pixel format that supports an alpha channel so that transparency composites correctly with the Motion_Background_Layer.

### Requirement 5: Motion Background Configuration on Song Items

**User Story:** As a worship team member, I want to assign a motion background video to a Song item, so that I can customise the visual presentation for each song.

#### Acceptance Criteria

1. THE Song_Item SHALL expose a configurable string property for specifying the file path of a Motion_Background_Video, accepting a value of null, empty string, or a file path up to 260 characters in length.
2. WHEN the Motion_Background_Video property is set to a file path that references an existing file with a supported video extension (.mp4, .mov, .avi, .wmv, .mkv, .webm), THE Song_Item SHALL use that video as the motion background during playback.
3. WHEN the Motion_Background_Video property is empty or null, THE Song_Item SHALL render without a motion background, using the standard Slide_Theme background instead. IF the background_type was previously set to motion video and the path becomes empty, THE Song_Item SHALL force switch to the standard Slide_Theme background.
4. IF the Motion_Background_Video property references a file that does not exist or is not a supported video format at playback time, THEN THE Song_Item SHALL fall back to rendering without a motion background, using the standard Slide_Theme background.
5. WHEN the playlist is saved, THE Song_Item SHALL persist the Motion_Background_Video file path as a relative path (relative to the playlist file directory).
6. WHEN the playlist is loaded, THE Song_Item SHALL restore the Motion_Background_Video configuration by resolving the stored relative path against the playlist file directory.

### Requirement 6: Motion Background Video File Validation

**User Story:** As a worship team member, I want the system to handle missing or invalid motion background video files gracefully, so that the presentation does not break during a service.

#### Acceptance Criteria

1. WHEN a Song_Item with a configured Motion_Background_Video becomes active AND the file path does not exist on disk, THEN THE Motion_Background_Layer SHALL render as transparent and log a warning that includes the configured file path.
2. WHEN a Song_Item with a configured Motion_Background_Video becomes active AND the file exists but cannot be decoded by the Motion_MpvContext, THEN THE Motion_Background_Layer SHALL render as transparent and log an error that includes the file path and the decoder-reported failure reason.
3. IF the Motion_Background_Video file becomes unreadable during playback (due to file deletion, access denial, or I/O error), THEN THE Motion_Background_Layer SHALL stop playback, render as transparent, and log a warning that includes the file path.
4. IF any Motion_Background_Video validation or playback error occurs, THEN THE Motion_Background_Layer SHALL NOT display an error dialog, throw an unhandled exception, or interrupt the active presentation output.

### Requirement 7: NDI and Stage Display Output

**User Story:** As a broadcast operator, I want the NDI output and stage display to include the motion background composited with the lyrics, so that all outputs match the projector display.

#### Acceptance Criteria

1. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Projector_Window NDI main output SHALL include the Motion_Background_Layer composited beneath the Content_Layer in the NDI frame.
2. THE Projector_Window NDI lyrics-only output SHALL NOT include the Motion_Background_Layer in any frame under any circumstances, regardless of whether a Motion_Background_Video is configured or whether the operator desires lyrics overlaid on the motion background.
3. WHILE a Song_Item with a configured Motion_Background_Video is active, THE Stage_Display_Window SHALL render the Motion_Background_Layer beneath the Content_Layer using the same compositing order as the Projector_Window.
4. THE Stage_Display_Window SHALL use its own Motion_MpvContext instance for motion background playback, independent of the Projector_Window Motion_MpvContext.
