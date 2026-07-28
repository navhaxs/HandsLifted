# Local USX Source Design

**Date:** 2026-07-26
**Status:** Approved, ready for planning
**Depends on:** Phases 1–4a of the Scripture Slide Type feature (`docs/superpowers/specs/2026-07-26-scripture-slide-type-design.md`), already merged on `feature/scripture-slide-type`.

## Background

`ScriptureSourceLoader` (Phase 1, `HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs`) checks a disk cache (`%APPDATA%\HandsLifted\ScriptureCache\<translation>\<book>.usx`) before fetching from `https://v1.fetch.bible/bibles/...` over HTTP. This means a scripture slide render can silently hit the network on any cache miss.

Phase 4a's final whole-branch review parked a finding on exactly this: `ItemInstanceFactoryTests.cs`'s round-trip test causes a real, unmocked network fetch as a side effect of running the test suite, because `ItemInstanceFactory.ToItemInstance` fire-and-forgets `ScriptureItemInstance.GenerateSlidesAsync()`, which goes through `ScriptureSourceLoader`. The finding was accepted as plan-mandated and deferred, with the explicit expectation that this design would resolve it at the source.

## Goal

Scripture slide rendering never touches the network. USX data is read from a local, user-configured directory only. A separate, explicit "download" action (network-using, triggered from app preferences) populates that directory ahead of time.

## Non-Goals

- Translation picker or multi-translation support — one fixed translation constant (`eng_bsb`, matching what Phases 1–4a already use in tests and docs), whole Bible (66 books).
- A first-run wizard — no such flow exists in the app today (`WelcomeWindow` is a recent-playlists launcher, not an onboarding wizard); building one is out of scope here.
- Resumable/paused downloads, retry-with-backoff on individual book failures — a failed book during download is logged, skipped, and the rest of the batch continues; re-clicking "Download" later re-fetches only the books still missing.

## Architecture

Two new classes replace `ScriptureSourceLoader` in `HandsLiftedApp.Importer.Scripture`:

### `ScriptureLocalUsxStore` (disk-only)

- `Task<ScriptureBook> LoadBookAsync(string bookCode)` — reads `<root>/<book>.usx`, parses via the existing `UsxScriptureParser`. No `HttpClient` field, no network type reachable from this class at all — this is an architectural guarantee that render time cannot reach the network through this component, not a runtime flag.
- Missing file throws a new `ScriptureBookNotFoundException` (rather than returning null), so "not found" can't be silently mistaken for "empty book" by a caller that forgets to check.
- Retains the in-memory `ConcurrentDictionary` cache and the book-code validation (`^[a-z0-9_]+$`, same pattern-based path-traversal guard) from today's loader.
- Constructor takes the root path directly (no translation subdirectory needed, since there is exactly one fixed translation for this phase — simpler than today's `<translation>/<book>.usx` layout).

### `ScriptureUsxDownloader` (network-only)

- `Task DownloadAllBooksAsync(string rootPath, IProgress<(int done, int total)>? progress, CancellationToken ct)` — fetches all 66 books from `fetch.bible` for the fixed translation constant, writes each to `<root>/<book>.usx` using the same atomic temp-file-then-move pattern `ScriptureSourceLoader` uses today.
- Skips books whose file already exists at `rootPath` (so re-running after a partial/failed download only fetches what's missing).
- A single book's fetch failure is logged and skipped; the batch continues rather than aborting.
- Used only by `SetupWindow`'s download button — never constructed anywhere in the render path.

`ScriptureSourceLoader.cs` and its test file are deleted outright, not deprecated in place — leaving it around as a third, unused path would just be dead code inviting a future regression back to the bug this design closes.

### `ScriptureItemInstance` changes

Constructor changes from `(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null)` to `(PlaylistInstance? parentPlaylist, ScriptureLocalUsxStore? store = null)`. When not injected, default-constructs a `ScriptureLocalUsxStore` pointed at `Globals.Instance.AppPreferences.ScriptureDataPath` — same optional-for-testing pattern as today.

## Data Flow

### Setup-time (download)

1. `SetupWindow`'s Library tab gains a "Scripture Data Path" `TextBox` bound to a new `AppPreferencesViewModel.ScriptureDataPath` property (`[DataMember]`, default `%APPDATA%\HandsLifted\ScriptureData`), following the exact pattern already used for `LibraryPath`/`GoogleClientId` in that window.
2. A "Download Bible Data" button constructs a `ScriptureUsxDownloader`, calls `DownloadAllBooksAsync(ScriptureDataPath, progress, ct)`, showing a "Downloading... N/66 books" indicator and disabling itself while running.
3. Errors (network down, individual book failure) surface inline in the tab; already-downloaded books are kept. The button can be re-clicked to retry/resume.
4. Nothing runs automatically on first launch — the path can sit unset indefinitely; download is opt-in whenever the user visits Setup.

### Render-time (read)

1. `ItemInstanceFactory.ToItemInstance`'s `ScriptureItem` branch constructs `ScriptureItemInstance(playlist)`, which internally builds a `ScriptureLocalUsxStore(AppPreferences.ScriptureDataPath)`.
2. `GenerateSlidesAsync` calls `_store.LoadBookAsync(Book)` — a pure disk read; no HTTP type is reachable from this call graph.
3. Success: same verse-slide generation as today (`ScriptureVerseRangeExtractor`, `UpdateVerseSlides`).
4. Missing file / unset path: `ScriptureBookNotFoundException` is caught locally and produces a placeholder error slide (see below) instead of propagating.

## Error Handling

`GenerateSlidesAsync` wraps the store call:

```csharp
public async Task GenerateSlidesAsync()
{
    try
    {
        var book = await _store.LoadBookAsync(Book);
        var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
        UpdateVerseSlides(book.Title, verses);
    }
    catch (ScriptureBookNotFoundException ex)
    {
        Log.Error(ex, "Scripture data not found for {Book} ({Translation})", Book, Translation);
        UpdateVerseSlides(Book, MakeMissingDataPlaceholder());
    }
}
```

- `MakeMissingDataPlaceholder()` produces one placeholder verse entry whose text reads: `"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\nCheck Setup > Library > Scripture Data Path"`. It's routed through the existing `UpdateVerseSlides` → `ScriptureSlideInstance` path, rendering through Phase 3's already-built `ScriptureSlideSpecBuilder` word-wrap/autofit — no new rendering code needed.
- This sits alongside Phase 4a's existing `ItemInstanceFactory`-level fault logging (`.ContinueWith(..., TaskContinuationOptions.OnlyOnFaulted)`), which remains the catch-all for any *other* unexpected exception (I/O error, corrupt file) — the try/catch here only narrows to the specific "not found" case.
- No other `IItemInstance` type has a placeholder-slide precedent to match, since none of them can fail this way — `SongItem` reads from the same playlist file, `MediaGroupItem`/etc. are local file references resolved at load time. This is a new pattern specific to scripture's "data lives outside the playlist file" model.

## Migration

Existing code touched:

- `HandsLiftedApp.Importer.Scripture/ScriptureSourceLoader.cs` and `HandsLiftedApp.Tests/Importer/Scripture/ScriptureSourceLoaderTests.cs` deleted, replaced by the two new classes and their own test files.
- `HandsLiftedApp.Core/Models/RuntimeData/Items/ScriptureItemInstance.cs`: constructor param type change; `GenerateSlidesAsync` gets the try/catch above.
- `HandsLiftedApp.Tests/Models/RuntimeData/Items/ScriptureItemInstanceTests.cs`: `MakeFakeLoader` (currently fakes `HttpClient` via `FakeHttpMessageHandler`) is replaced by a helper that writes the fixture `.usx` string to a temp directory and constructs `ScriptureLocalUsxStore(tempDir)` directly — no HTTP faking needed at all.
- `HandsLiftedApp.Data/Models/Items/ScriptureItem.cs`'s doc comment (currently explains the no-cache rationale by referencing `ScriptureSourceLoader`) gets a one-line update to name the new store.
- `HandsLiftedApp.Core/ViewModels/AppPreferencesViewModel.cs`: new `ScriptureDataPath` property.
- `HandsLiftedApp.Core/Views/Setup/SetupWindow.axaml` (+ `.axaml.cs`): new field + button in the Library tab.

This also resolves Phase 4a's parked Task 3 finding: `ItemInstanceFactoryTests.cs`'s round-trip test goes through `ItemInstanceFactory.ToItemInstance`, which will now construct a `ScriptureLocalUsxStore` against whatever `AppPreferences.ScriptureDataPath` is in the test host (unset/empty) — it hits the missing-file path and logs-and-placeholders instead of making a real network call. No test change is needed there; removing the network call is a side effect of this architecture change.

## Testing

- `ScriptureLocalUsxStore`: reads an existing file; missing file throws `ScriptureBookNotFoundException`; memory-cache behavior (carried over from today's loader tests); book-code validation / path-traversal guard (carried over).
- `ScriptureUsxDownloader`: fetches and writes one book (fake `HttpClient` via the existing `FakeHttpMessageHandler`); skips already-present books on re-run; a single-book failure doesn't abort the rest of the batch.
- `ScriptureItemInstance`: existing 5 tests migrate to construct `ScriptureLocalUsxStore` instead of faking HTTP; one new test covers the missing-file → placeholder-slide path.
