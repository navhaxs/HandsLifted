# Patterns & Gotchas

## File Path Handling

- **PlaylistWorkingDirectory defaults to a relative path** (`VisionScreensUserData\`). It only becomes absolute when a playlist is loaded from disk. Any code that resolves relative paths against it must handle this gracefully.
- **`RelativeFilePathResolver` guards against relative base paths** — both `ToAbsolutePath` and `ToRelativePath` check `Path.IsPathFullyQualified(relativeTo)`. If the base is relative, they return the path unchanged. This prevents garbage paths like `VisionScreensUserData\..\Users\...`.
- **Validate paths are absolute before using them** — `MotionBackgroundService.IsValidVideoFile` rejects relative paths. Any feature that stores file paths should validate with `Path.IsPathFullyQualified` before treating them as usable.
- **`RelativeFilePathResolver.ToAbsolutePath`** resolves relative paths against a base directory. It calls `Path.GetFullPath` internally — use it rather than raw `Path.Combine` for file path resolution.
- **`RelativeFilePathResolver.ToRelativePath`** converts absolute paths to relative for serialization. Always pair with `ToAbsolutePath` on deserialization.

## Serialization (XML Playlist Files)

- File paths in XML are stored **relative to the playlist directory**. On save, convert absolute → relative via `RelativeFilePathResolver.ToRelativePath`. On load, convert relative → absolute via `RelativeFilePathResolver.ToAbsolutePath`.
- Serialization happens in `HandsLiftedDocXmlSerializer.SerializeItem`. Deserialization happens in `ItemInstanceFactory.ToItemInstance`.
- When adding a new file path property to a data model, you must update **both** the serializer and the factory.
- **Stale relative paths in saved files** — If a path was saved incorrectly (e.g., before the resolver guards were added), it will remain broken until the user re-selects the file. The app should handle this gracefully (reject invalid paths, don't crash).

## SoftwareVideoView & MpvContext

- `SoftwareVideoView` uses a **shared bitmap pattern** — multiple views can share one `MpvContext`, but only the "primary renderer" (first to register) updates the `WriteableBitmap`.
- Each `SoftwareVideoView` instance that uses its own `MpvContext` becomes its own primary renderer. This is the pattern used by `MotionBackgroundLayer` (one context per output window).
- The `MpvContext` property on `SoftwareVideoView` is a `DirectProperty` — set it to connect/disconnect the view from a context.
- **First frame detection**: Subscribe to `MpvContext.VideoReconfig` event. It fires when the video dimensions are configured (i.e., first frame is decoded and ready to render).
- **Error detection during playback**: Subscribe to `MpvContext.EndFile` event. Check `e.Reason == MPV_END_FILE_REASON_ERROR`.

## NDI Output

- **NDI video buffer shortcut** — `NDISendContainer` has an optimization: it searches for `IGetVideoBufferBitmap` controls in the visual tree. If exactly ONE is found, it grabs the video bitmap directly (bypassing the full Avalonia render). If multiple are found (e.g., motion background + video slide), it falls back to `rtb.Render(this.Child)` which composites all layers correctly.
- **Adding a new SoftwareVideoView to the NDI container tree** will disable the video shortcut (count > 1) and force full composited rendering. This is correct behaviour — the shortcut only works for single-video-only content.
- **NullReferenceException in CopyPixels** — Can occur when a `WriteableBitmap`'s internal buffer isn't initialized yet. The NDI render loop catches this silently and skips the frame. Non-fatal.
- **NDI main output** includes everything inside the `NDISendContainer` Grid (motion background + content + video layer).
- **NDI lyrics-only output** uses `AltSlideRenderer` in a separate `NDISendContainer` — it does NOT include motion backgrounds or video layers.

## Compositing & Layer Stack

- **ProjectorWindow compositing order** (back to front): Black background Grid → MotionBackgroundLayer → ActiveSlideRender → VideoLayerRenderer.
- **ActiveSlideRender has `Background="Transparent"`** — This is required so the motion background video shows through. Do NOT set it back to black.
- **Slide bitmaps must be transparent when motion background is active** — The `SlideRendererWorkerWindow` forces `Grid.Background = Transparent` and `Border.Background = null` on `SongSlideView` when `HasMotionBackground == true`, after bindings resolve but before `rtb.Render()`.
- **AXAML MultiBinding may not resolve during off-screen rendering** — The `SlideRendererWorkerWindow` creates controls, adds them to a hidden window, runs layout, then renders. Compiled bindings to `HasMotionBackground` may not evaluate in time. The explicit code-behind override in the renderer is the reliable path.

## Slide Transitions (XFade)

- **XFade has two modes** controlled by `CrossDissolve` property:
  - `false` (default): New slide fades in (0→1) over the stationary old slide. Old is hidden after transition completes. This is the PowerPoint-style fade — works for opaque slides.
  - `true`: Old slide fades out (1→0) simultaneously with new slide fading in (0→1). This is the ProPresenter-style cross-dissolve — required for transparent slides over motion backgrounds.
- **ActiveSlideRender toggles CrossDissolve** based on whether the incoming slide has `HasMotionBackground == true`.
- **Never fade out the old slide for opaque content** — Simultaneous fade-out + fade-in with opaque slides causes a "dip to black" because at the midpoint both slides are at 50% opacity, revealing the transparent/black layer behind.
- **Always cross-dissolve for transparent content** — Fade-in-only with transparent slides causes "double text" where old and new lyrics are both fully visible simultaneously.

## Reactive Property Change Propagation

- When a computed property (e.g., `HasMotionBackground`) depends on another property, you must explicitly raise `PropertyChanged` for the computed property when the source changes. Use `WhenAnyValue` + `Subscribe` + `RaisePropertyChanged`.
- **Delegated properties across parent/child** (e.g., `SongSlideInstance.HasMotionBackground` delegates to `SongItemInstance.HasMotionBackground`): The child does NOT automatically re-notify when the parent's property changes. If the child is used as a DataContext for bindings, the binding won't update unless the child raises its own `PropertyChanged`. For off-screen bitmap rendering this is fine (fresh render picks up current value), but for live UI bindings it can be stale.

## Bitmap Rendering Pipeline

- Song slide bitmaps are rendered off-screen by `SlideRendererWorkerWindow` using `SongSlideView` as the template.
- The DataContext during rendering is a `SongSlideInstance`. Any property the view binds to must be accessible from this instance.
- **Bitmap regeneration** is triggered by sending `SlideRenderRequestMessage` via `MessageBus.Current.SendMessage`. Each message targets one slide.
- When adding a property that affects slide appearance, ensure bitmap regeneration is triggered when the property changes (see `RegenerateAllSlideBitmaps` pattern in `SongItemInstance`).
- **Timing issue with regeneration on load** — When a playlist is loaded, `MotionBackgroundVideoPath` may be set before slides are generated. The `RegenerateAllSlideBitmaps` call fires on an empty `Slides` collection. The slides are later generated via `UpdateStanzaSlides()` which creates new `SongSlideInstance` objects that call `GenerateBitmaps()` in their constructor — at which point `HasMotionBackground` is already true, so they render correctly.

## UI Patterns

- **File picker**: Use `TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(...)`. Always null-check the TopLevel.
- **Editor tabs**: `SongEditorControl` has a left-side `TabControl`. New editor sections are added as `TabItem` entries.
- **Conditional rendering in AXAML**: Use `IMultiValueConverter` with `MultiBinding` when a visual property depends on multiple data values (e.g., `MotionBackgroundBrushConverter` checks `HasMotionBackground` + theme colour).

## Common Pitfalls

1. **Adding a file path property without updating serialization** — The property will work at runtime but won't persist across save/load cycles.
2. **Assuming PlaylistWorkingDirectory is absolute** — It's relative until a playlist file is opened. Always use `RelativeFilePathResolver` which handles this.
3. **Forgetting to trigger bitmap regeneration** — If a property affects slide rendering but doesn't trigger `SlideRenderRequestMessage`, cached bitmaps will show stale content.
4. **Setting Background="Black" on compositing layers** — Any control in the layer stack between the motion background and the viewer must have `Background="Transparent"` or the video won't show through.
5. **NDI video shortcut with multiple SoftwareVideoViews** — Adding a new `SoftwareVideoView` (e.g., motion background) to the NDI container tree disables the single-video shortcut. The full render path handles compositing correctly.
6. **XFade mode mismatch** — Using simultaneous fade for opaque slides causes dip-to-black. Using fade-in-only for transparent slides causes double-text. The mode must match the content type.
7. **Relative paths passing IsValidVideoFile** — Always check `Path.IsPathFullyQualified` for file paths that will be used with `File.Exists` or passed to external libraries. Relative paths with valid extensions will pass extension-only checks but fail at runtime.
