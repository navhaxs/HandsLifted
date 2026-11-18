# Theme background opacity — design

## Intent
A slide theme gets a "Background opacity" slider. It controls how strongly the theme's background image or motion video shows. The theme's background colour is the bottom layer behind it, so the two blend. Purpose: dim/tint busy backgrounds so lyrics stay readable.

## Goals
- `BaseSlideTheme.BackgroundOpacity`, 0–100 (percent), default 100. Old theme files load unchanged (missing element => 100).
- Image themes: colour drawn first, image drawn on top at `opacity`.
- Motion-video themes: video keeps playing in its existing layer; the canvas draws the colour over it at alpha `(1 - opacity)`. Source-over math: `colour·(1−o) + video·o` = video at opacity `o` over colour.
- At 100 every existing opaque-image/video theme renders identically.
- Slider in the theme designer; undoable.

## Non-goals
- Custom slide backgrounds (not `BaseSlideTheme`).
- Thumbnails of generated motion song slides get no special handling: identical at 100%, and below 100% they show the translucent colour tint (a side effect of approach A, harmless). Only the title-slide video-frame thumbnail gets the real blend.
- Any change to `MotionBackgroundLayer` / `MotionBackgroundObserver` / `MotionBackgroundService` (one-layer ownership rule untouched).

## Approaches
- **A. Skia overlay (chosen).** Motion slides return `SolidBackground(colour, alpha=1-o)` from the spec builders. All windows already have a `SlideCanvas`. No layer/observer/service change.
- **B. Colour + opacity inside the video controls** via a new `MotionBackgroundService` subject. Fades in sync with the video, but adds plumbing in the sensitive libmpv layers. Rejected.

Known trade-off of A: the colour tint fades on the slide-transition timing; the video layer has its own 500ms fade, so the tint can lead/lag the video by up to 500ms. End state is identical.

## Calls made by me
- Stored as int percent, not decimal.
- Slider enabled only when an image is set or `BackgroundMode == MotionBackground`.
- Image path always composites the colour underneath (even at 100). Existing themes only change if colour is non-black AND the image has transparency — then the colour now shows through, which is the intent.
- Added to `ThemeEditRecorder` snapshot for undo.

## Left to the builder
Exact `NumericSliderBox` binding for an int, the `ImageBackground`/`SkiaBitmapBackground` record shape (optional trailing params, so image-slide and logo callers are untouched), designer preview behaviour.
