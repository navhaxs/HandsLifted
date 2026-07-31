# Portable Playlists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a playlist folder fully self-contained and movable to another PC: every directly-imported media file, theme background graphic, logo, and PDF/PPTX source document gets copied into the playlist's own folder tree on add, and rendered PDF/PPTX/Google-Slides PNG exports move out of the playlist folder entirely into a per-machine, content-hash-keyed cache that regenerates automatically on first load elsewhere.

**Architecture:** A new `PortableAssetCopier` utility (collision-safe, idempotent copy-into-subfolder) is wired into the single shared media/presentation add path (`CreateItem`/`PlaylistInstance`'s `AddItemByFilePathMessage` handler) and into the two theme/logo picker code-behinds. `HandsLiftedDocXmlSerializer` stops baking per-slide export paths and the presentation source's export directory into `playlist.xml`, and fixes a save-side bug where the source presentation file was never actually relativized. The existing-but-unused `ImportCacheService` is rekeyed from absolute-path hashing (breaks across machines) to file-content hashing (survives a move), and the three regenerable group types' `Sync()` methods are repointed at it instead of a timestamped subfolder of the playlist directory. `MainViewModel`'s playlist-load path eagerly re-triggers `Sync()` for any such item whose cached/baked slides aren't present on this machine.

**Tech Stack:** net10.0, MSTest, Avalonia, ReactiveUI.

## Global Constraints

- Portable unit is a plain folder (`playlist.xml` + subfolders) the user copies/zips themselves — no container/archive format.
- Folder layout: `Media/Images/`, `Media/Video/`, `Themes/Backgrounds/`, `Themes/Logo/`, `Sources/`. No `Media/Audio/` — there is no standalone-audio import path in this codebase today (`Constants.cs` has no `SUPPORTED_AUDIO`); adding an unused folder would be speculative.
- Naming/collision rule: copies keep the original filename. Only a genuine clash — a *different* file already at that name — gets an 8-hex-char content-hash suffix appended (`IMG_0001_a3f9c2d1.jpg`). Re-adding the exact same file (same name, same bytes) reuses the existing copy rather than duplicating it — the simplest interpretation of "no dedup logic" that doesn't require deleting/comparing across the whole media pool.
- Screen/monitor configuration (`AppPreferences`) is out of scope — stays machine-local, untouched.
- The global default theme (`AppPreferences.DefaultTheme`) and the global default logo (`AppPreferences.LogoGraphicFile`) are also out of scope — they are shared/machine-local and must NOT be copied into any single playlist's folder. Every task touching theme/logo graphics must guard against the theme-in-question being the shared default (compare `.Id` against `AppPreferences.DefaultTheme?.Id`, matching the existing filter in `HandsLiftedDocXmlSerializer.cs:44`).
- Google Slides items have no local source file (fetched by presentation ID) — they are unaffected by the copy-in-original-source part of this work, only by the cache-rekeying part (their existing ID-based cache key is already stable across machines).
- Church PC has the same PowerPoint/converter tooling as the authoring PC — safe to assume PDF/PPTX regeneration always succeeds wherever the playlist is opened.
- Current baseline: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo` passes 209 tests before this plan starts.
- Design spec: [docs/superpowers/specs/2026-07-31-portable-playlists-design.md](../specs/2026-07-31-portable-playlists-design.md).

---

### Task 1: `PortableAssetCopier` utility

**Files:**
- Create: `HandsLiftedApp.Core/Utils/PortableAssetCopier.cs`
- Test: `HandsLiftedApp.Tests/PortableAssetCopierTests.cs`

**Interfaces:**
- Produces:
  - `public static string CopyIntoSubfolder(string sourceFilePath, string playlistWorkingDirectory, string relativeSubfolder)` — copies (or reuses an identical existing copy of) `sourceFilePath` into `Path.Combine(playlistWorkingDirectory, relativeSubfolder)`, returns the absolute path of the copy. Used directly by Task 3 (theme/logo pickers).
  - `public static string CopyMediaOrPresentationIntoPlaylist(string filePath, string playlistWorkingDirectory)` — routes by extension into `Media/Images`, `Media/Video`, or `Sources` via `CopyIntoSubfolder`; returns `filePath` unchanged for any other extension (e.g. `.txt`/`.xml` songs/scripture, which carry no file reference at all). Used by Task 2.
- Consumes: `HandsLiftedApp.Core.Constants.SUPPORTED_IMAGE/SUPPORTED_VIDEO/SUPPORTED_PDF/SUPPORTED_POWERPOINT` (internal, same assembly — visible via enclosing-namespace lookup, no `using` needed since `Constants` lives in the parent `HandsLiftedApp.Core` namespace).

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/PortableAssetCopierTests.cs`:

```csharp
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Tests;

[TestClass]
public class PortableAssetCopierTests
{
    private string _tempDir = null!;
    private string _playlistDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PortableAssetCopierTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _playlistDir = Path.Combine(_tempDir, "Playlist");
        Directory.CreateDirectory(_playlistDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteSourceFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void CopyIntoSubfolder_NewFile_CopiesKeepingOriginalName()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");

        var result = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Images", "photo.jpg"), result);
        Assert.IsTrue(File.Exists(result));
        Assert.AreEqual("photo-bytes", File.ReadAllText(result));
    }

    [TestMethod]
    public void CopyIntoSubfolder_SameNameSameContent_ReusesExistingCopyWithoutDuplicating()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");
        var first = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        var second = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(first, second);
        var destDir = Path.Combine(_playlistDir, "Media", "Images");
        Assert.AreEqual(1, Directory.GetFiles(destDir).Length);
    }

    [TestMethod]
    public void CopyIntoSubfolder_SameNameDifferentContent_AppendsHashSuffix()
    {
        var sourceA = WriteSourceFile("photo.jpg", "photo-bytes-A");
        var destA = PortableAssetCopier.CopyIntoSubfolder(sourceA, _playlistDir, Path.Combine("Media", "Images"));

        Directory.CreateDirectory(Path.Combine(_tempDir, "OtherFolder"));
        var sourceB = Path.Combine(_tempDir, "OtherFolder", "photo.jpg");
        File.WriteAllText(sourceB, "photo-bytes-B-different");

        var destB = PortableAssetCopier.CopyIntoSubfolder(sourceB, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreNotEqual(destA, destB);
        Assert.IsTrue(File.Exists(destA));
        Assert.IsTrue(File.Exists(destB));
        Assert.AreEqual("photo-bytes-A", File.ReadAllText(destA));
        Assert.AreEqual("photo-bytes-B-different", File.ReadAllText(destB));
        StringAssert.StartsWith(Path.GetFileName(destB), "photo_");
    }

    [TestMethod]
    public void CopyIntoSubfolder_SourceAlreadyInsideDestination_IsNoOp()
    {
        var source = WriteSourceFile("photo.jpg", "photo-bytes");
        var firstCopy = PortableAssetCopier.CopyIntoSubfolder(source, _playlistDir, Path.Combine("Media", "Images"));

        var result = PortableAssetCopier.CopyIntoSubfolder(firstCopy, _playlistDir, Path.Combine("Media", "Images"));

        Assert.AreEqual(firstCopy, result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Image_RoutesToMediaImages()
    {
        var source = WriteSourceFile("photo.png", "png-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Images", "photo.png"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Video_RoutesToMediaVideo()
    {
        var source = WriteSourceFile("clip.mp4", "mp4-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Media", "Video", "clip.mp4"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Pdf_RoutesToSources()
    {
        var source = WriteSourceFile("sermon.pdf", "pdf-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Sources", "sermon.pdf"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_Pptx_RoutesToSources()
    {
        var source = WriteSourceFile("sermon.pptx", "pptx-bytes");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(Path.Combine(_playlistDir, "Sources", "sermon.pptx"), result);
    }

    [TestMethod]
    public void CopyMediaOrPresentationIntoPlaylist_SongXml_ReturnsPathUnchanged()
    {
        var source = WriteSourceFile("song.xml", "<Song />");

        var result = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(source, _playlistDir);

        Assert.AreEqual(source, result);
        Assert.IsFalse(Directory.Exists(Path.Combine(_playlistDir, "Media")));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~PortableAssetCopierTests"`
Expected: FAIL — compile error, `PortableAssetCopier` doesn't exist yet.

- [ ] **Step 3: Implement `PortableAssetCopier`**

`HandsLiftedApp.Core/Utils/PortableAssetCopier.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace HandsLiftedApp.Core.Utils
{
    public static class PortableAssetCopier
    {
        public static string CopyIntoSubfolder(string sourceFilePath, string playlistWorkingDirectory, string relativeSubfolder)
        {
            var destDir = Path.Combine(playlistWorkingDirectory, relativeSubfolder);
            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = Path.Combine(destDir, fileName);

            if (File.Exists(destPath))
            {
                if (FilesAreIdentical(sourceFilePath, destPath))
                {
                    return destPath;
                }

                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var suffix = ComputeFileHash(sourceFilePath)[..8];
                destPath = Path.Combine(destDir, $"{nameNoExt}_{suffix}{ext}");

                if (File.Exists(destPath) && FilesAreIdentical(sourceFilePath, destPath))
                {
                    return destPath;
                }
            }

            File.Copy(sourceFilePath, destPath, overwrite: true);
            return destPath;
        }

        public static string CopyMediaOrPresentationIntoPlaylist(string filePath, string playlistWorkingDirectory)
        {
            var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            if (Constants.SUPPORTED_IMAGE.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, Path.Combine("Media", "Images"));
            }

            if (Constants.SUPPORTED_VIDEO.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, Path.Combine("Media", "Video"));
            }

            if (Constants.SUPPORTED_PDF.Contains(ext) || Constants.SUPPORTED_POWERPOINT.Contains(ext))
            {
                return CopyIntoSubfolder(filePath, playlistWorkingDirectory, "Sources");
            }

            return filePath;
        }

        private static bool FilesAreIdentical(string pathA, string pathB)
        {
            if (new FileInfo(pathA).Length != new FileInfo(pathB).Length)
            {
                return false;
            }

            return ComputeFileHash(pathA) == ComputeFileHash(pathB);
        }

        private static string ComputeFileHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~PortableAssetCopierTests"`
Expected: PASS — 9 tests.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Utils/PortableAssetCopier.cs HandsLiftedApp.Tests/PortableAssetCopierTests.cs
git commit -m "feat: add PortableAssetCopier for copy-into-playlist-folder semantics"
```

---

### Task 2: Wire copier into the media/presentation add flow

**Files:**
- Modify: `HandsLiftedApp.Core/Models/PlaylistInstance.cs:178-181`
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:303-306`

**Interfaces:**
- Consumes: `PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(string filePath, string playlistWorkingDirectory)` from Task 1.

No new automated test: this wires Task 1's already-fully-unit-tested routing logic into two existing call sites; the behavior added is "call a pure function on a path before passing it on," not new logic in its own right. Verified by the existing test suite staying green (both call sites' surrounding logic is otherwise unit-tested already via `CreateItemTests`/`ItemInstanceFactoryTests`) plus Task 8's manual end-to-end check.

- [ ] **Step 1: Wire the `AddItemByFilePathMessage` handler**

In `HandsLiftedApp.Core/Models/PlaylistInstance.cs`, inside the `MessageBus.Current.Listen<AddItemByFilePathMessage>()` subscription, change:

```csharp
                    foreach (var filePath in itemsToInsert)
                    {
                        var newItem = CreateItem.GenerateItem(filePath);
```

to:

```csharp
                    foreach (var filePath in itemsToInsert)
                    {
                        var localizedFilePath = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(
                            filePath, PlaylistWorkingDirectory);
                        var newItem = CreateItem.GenerateItem(localizedFilePath);
```

(`HandsLiftedApp.Core.Utils` is already imported in this file.) This is the single shared entry point for the "Add Item" browse dialog, drag-and-drop, and picking a result from any Library (including a media-bin Library) — see [2026-07-31-portable-playlists-design.md](../specs/2026-07-31-portable-playlists-design.md) for why this one hook covers all three.

- [ ] **Step 2: Wire the dedicated "Add Presentation" flow**

In `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, change:

```csharp
                        if (filePaths.Count > 0)
                        {
                            itemToInsert = CreateItem.OpenPresentationFile(filePaths[0].TryGetLocalPath(), Playlist);
                        }
```

to:

```csharp
                        if (filePaths.Count > 0)
                        {
                            var localizedPresentationPath = PortableAssetCopier.CopyMediaOrPresentationIntoPlaylist(
                                filePaths[0].TryGetLocalPath(), Playlist.PlaylistWorkingDirectory);
                            itemToInsert = CreateItem.OpenPresentationFile(localizedPresentationPath, Playlist);
                        }
```

(`HandsLiftedApp.Core.Utils` is already imported in this file.)

- [ ] **Step 3: Build and run the full suite to confirm no regression**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 218 tests (209 baseline + 9 from Task 1), 0 failed.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Models/PlaylistInstance.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "feat: copy media/presentation files into playlist folder on add"
```

---

### Task 3: Copy theme background graphic and logo into the playlist folder on pick

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs:356-397`
- Modify: `HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml:58`
- Modify: `HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml.cs`

**Interfaces:**
- Consumes: `PortableAssetCopier.CopyIntoSubfolder` from Task 1.

Both edits are UI code-behind reachable only through a live file-picker dialog — there is no Avalonia UI test harness in this codebase (same precedent noted in `docs/superpowers/plans/2026-07-30-scripture-edit-button.md`'s Global Constraints). Verified by build succeeding plus a manual click-through in Task 8, not an automated test. Both edits must skip the copy when the theme/logo in question is the shared, machine-local default (`AppPreferences.DefaultTheme` / `AppPreferences.LogoGraphicFile`) — copying the shared default's file into one specific playlist's folder and rewriting the shared object's path would corrupt that default for every other playlist.

- [ ] **Step 1: Theme background graphic — `SlideThemeDesigner.axaml.cs`**

Add `using HandsLiftedApp.Core.Utils;` to the top of the file, then change `ChangeThemeBgGraphic_OnClick`:

```csharp
        private async void ChangeThemeBgGraphic_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var filePaths = await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(
                    new FilePickerOpenOptions()
                    {
                        AllowMultiple = false,
                        Title = "Select Background Graphic",
                        FileTypeFilter = new List<FilePickerFileType>()
                        {
                            new FilePickerFileType("Image Files")
                            {
                                Patterns = new List<string>()
                                {
                                    "*.png",
                                    "*.jpg",
                                    "*.jpeg",
                                    "*.bmp"
                                }
                            },
                            new FilePickerFileType("All Files")
                            {
                                Patterns = new List<string>()
                                {
                                    "*.*"
                                }
                            }
                        }
                    });
                if (filePaths == null || filePaths.Count == 0) return;

                var localPath = filePaths[0].TryGetLocalPath();
                if (AssetLoader.Exists(filePaths[0].Path) || File.Exists(localPath))
                {
                    if (localPath != null && File.Exists(localPath))
                    {
                        var selectedTheme = designsListBox.SelectedItem as BaseSlideTheme;
                        var isSharedDefaultTheme = selectedTheme != null
                            && selectedTheme.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id;

                        if (!isSharedDefaultTheme)
                        {
                            localPath = PortableAssetCopier.CopyIntoSubfolder(
                                localPath,
                                Globals.Instance.MainViewModel.Playlist.PlaylistWorkingDirectory,
                                Path.Combine("Themes", "Backgrounds"));
                        }
                    }

                    bgGraphicFilePath.Text = localPath;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error changing theme background graphic");
            }
        }
```

- [ ] **Step 2: Logo — name the playlist-scoped picker**

In `HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml`, change:

```xml
                <controls:TextBoxFilePathPicker FilePath="{Binding Playlist.LogoGraphicFile}" />
```

to:

```xml
                <controls:TextBoxFilePathPicker x:Name="playlistLogoPicker" FilePath="{Binding Playlist.LogoGraphicFile}" />
```

(Leave the `AppPreferences.LogoGraphicFile`-bound picker above it untouched — that one is the shared, machine-local global default and must never be copied into a playlist folder.)

- [ ] **Step 3: Logo — copy into `Themes/Logo` on change**

`HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml.cs`:

```csharp
using System;
using System.IO;
using Avalonia.Controls;
using HandsLiftedApp.Core.Controls;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class LogoEditorView : UserControl
    {
        public LogoEditorView()
        {
            InitializeComponent();

            playlistLogoPicker.GetObservable(TextBoxFilePathPicker.FilePathProperty).Subscribe(path =>
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                if (Globals.Instance.MainViewModel?.Playlist == null) return;

                var copiedPath = PortableAssetCopier.CopyIntoSubfolder(
                    path,
                    Globals.Instance.MainViewModel.Playlist.PlaylistWorkingDirectory,
                    Path.Combine("Themes", "Logo"));

                if (copiedPath != path)
                {
                    playlistLogoPicker.FilePath = copiedPath;
                }
            });
        }
    }
}
```

The `if (copiedPath != path)` guard combined with `CopyIntoSubfolder`'s existing idempotent-if-already-copied-in behavior (Task 1) means this fires at most twice per pick (raw path in, copied path back out, no further change) — no infinite loop.

- [ ] **Step 4: Build**

Run: `dotnet build HandsLiftedApp.sln --nologo`
Expected: Build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml HandsLiftedApp.Core/Views/Designer/LogoEditorView.axaml.cs
git commit -m "feat: copy theme background graphic and playlist logo into playlist folder on pick"
```

---

### Task 4: Fix `HandsLiftedDocXmlSerializer` — relative source path, stop baking derived exports

**Files:**
- Modify: `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs:178-279`
- Test: `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`

**Interfaces:**
- Consumes: `RelativeFilePathResolver.ToRelativePath` (existing).
- Produces: no new public surface — behavior change only, in `SerializeItem`'s `PowerPointPresentationItemInstance`, `GoogleSlidesGroupItemInstance`, and `PDFSlidesGroupItemInstance` branches.

This task has two effects: (1) `SourcePresentationFile` is now correctly written as a relative path (it's a genuine bug fix — today's code calls `ToAbsolutePath` on save, which is a no-op today only because the source file happens to live outside the playlist folder; Task 2 makes it live inside the folder, so this must become `ToRelativePath` or the path silently stops resolving on another machine). (2) `Items` (the per-slide exported PNG list) and `SourceSlidesExportDirectory` are no longer written for these three group types at all — they become fully derived at load time (Task 6/7).

- [ ] **Step 1: Write the failing test**

Add to `HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs`:

```csharp
    [TestMethod]
    public void SerializePlaylist_PdfItem_SourcePresentationFileIsRelative_ItemsAndExportDirNotWritten()
    {
        var playlist = new PlaylistInstance();
        var sourcesDir = Path.Combine(_tempDir, "Sources");
        Directory.CreateDirectory(sourcesDir);
        var sourceFile = Path.Combine(sourcesDir, "sermon.pdf");
        File.WriteAllText(sourceFile, "pdf-bytes");

        var pdfInstance = new PDFSlidesGroupItemInstance(playlist)
        {
            Title = "Sermon Slides",
            SourcePresentationFile = sourceFile,
            SourceSlidesExportDirectory = Path.Combine(_tempDir, "SomeOldExportDir")
        };
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = Path.Combine(_tempDir, "SomeOldExportDir", "slide1.png") });
        playlist.Items.Add(pdfInstance);

        var path = Path.Combine(_tempDir, "playlist-pdf.xml");
        HandsLiftedDocXmlSerializer.SerializePlaylist(playlist, path);

        var rawXml = File.ReadAllText(path);
        StringAssert.Contains(rawXml, @"Sources\sermon.pdf");
        Assert.IsFalse(rawXml.Contains("SomeOldExportDir"), "Stale export directory/baked slide paths must not be written.");

        var deserialized = HandsLiftedDocXmlSerializer.DeserializePlaylist(path);
        var pdfItem = (PDFSlidesGroupItem)deserialized.Items.Single();
        Assert.AreEqual(0, pdfItem.Items.Count);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SerializePlaylist_PdfItem_SourcePresentationFileIsRelative"`
Expected: FAIL — `SourceSlidesExportDirectory`/`SomeOldExportDir` still present in the XML, and `SourcePresentationFile` written as an absolute path.

- [ ] **Step 3: Fix `SerializeItem`**

In `HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs`, in the `PowerPointPresentationItemInstance` branch, change:

```csharp
                return new PowerPointPresentationItem()
                {
                    UUID = powerPointPresentationItemInstance.UUID,
                    Title = powerPointPresentationItemInstance.Title,
                    Items = new TrulyObservableCollection<MediaGroupItem.GroupItem>(powerPointPresentationItemInstance
                        .Items
                        .Select(item =>
                        {
                            if (item is MediaGroupItem.MediaItem mediaItem)
                            {
                                // TODO deep copy
                                var newMediaItem = new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = mediaItem.SourceMediaFilePath, Meta = mediaItem.Meta };
                                if (newMediaItem.SourceMediaFilePath != null)
                                {
                                    newMediaItem.SourceMediaFilePath =
                                        RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                                            mediaItem.SourceMediaFilePath);
                                }

                                return newMediaItem;
                            }

                            return item;
                        }).ToList()),
                    AutoAdvanceTimer = powerPointPresentationItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItemInstance.SourcePresentationFile),
                    SourceSlidesExportDirectory = RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        powerPointPresentationItemInstance.SourceSlidesExportDirectory),
                    SlideTransitionDurationMs = powerPointPresentationItemInstance.SlideTransitionDurationMs
                };
```

to:

```csharp
                return new PowerPointPresentationItem()
                {
                    UUID = powerPointPresentationItemInstance.UUID,
                    Title = powerPointPresentationItemInstance.Title,
                    AutoAdvanceTimer = powerPointPresentationItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                        powerPointPresentationItemInstance.SourcePresentationFile),
                    SlideTransitionDurationMs = powerPointPresentationItemInstance.SlideTransitionDurationMs
                };
```

(Dropping the `Items`/`SourceSlidesExportDirectory` assignments leaves them at their class defaults — an empty collection and `null` respectively — which is exactly what should be written now that they're fully derived at load time.)

Apply the identical shape of change to the `PDFSlidesGroupItemInstance` branch:

```csharp
            else if (item is PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance)
            {
                return new PDFSlidesGroupItem()
                {
                    UUID = pdfSlidesGroupItemInstance.UUID,
                    Title = pdfSlidesGroupItemInstance.Title,
                    AutoAdvanceTimer = pdfSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourcePresentationFile = RelativeFilePathResolver.ToRelativePath(playlistDirectoryPath,
                        pdfSlidesGroupItemInstance.SourcePresentationFile),
                    SlideTransitionDurationMs = pdfSlidesGroupItemInstance.SlideTransitionDurationMs
                };
            }
```

And the `GoogleSlidesGroupItemInstance` branch (no `SourcePresentationFile` to fix there — only drop `Items`/`SourceSlidesExportDirectory`):

```csharp
            else if (item is GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance)
            {
                return new GoogleSlidesGroupItem()
                {
                    UUID = googleSlidesGroupItemInstance.UUID,
                    Title = googleSlidesGroupItemInstance.Title,
                    AutoAdvanceTimer = googleSlidesGroupItemInstance.AutoAdvanceTimer,
                    SourceGooglePresentationId = googleSlidesGroupItemInstance.SourceGooglePresentationId,
                    SlideTransitionDurationMs = googleSlidesGroupItemInstance.SlideTransitionDurationMs
                };
            }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SerializePlaylist_PdfItem_SourcePresentationFileIsRelative"`
Expected: PASS.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 219 tests, 0 failed. (This removes `Items`/`SourceSlidesExportDirectory` from freshly-serialized XML for these three types — confirm no existing test asserted on those fields for a freshly-serialized, as opposed to hand-constructed/legacy, playlist. None of the existing tests in `HandsLiftedDocXmlSerializerTests.cs` do.)

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/HandsLiftedDocXmlSerializer.cs HandsLiftedApp.Tests/HandsLiftedDocXmlSerializerTests.cs
git commit -m "fix: relativize PDF/PPTX source path on save, stop baking derived slide exports"
```

---

### Task 5: `ImportCacheService` — key by file content, not absolute path

**Files:**
- Modify: `HandsLiftedApp.Core/Services/ImportCacheService.cs`
- Test: `HandsLiftedApp.Tests/ImportCacheServiceTests.cs`

**Interfaces:**
- Produces (signature unchanged, behavior changed): `public static string GetFileImportCacheDirectory(string sourceFilePath)` — now returns the same directory for the same file content regardless of the file's absolute path. `GetKeyedCacheDirectory(string keyType, string key)` is unchanged (already path-independent).

- [ ] **Step 1: Write the failing tests**

`HandsLiftedApp.Tests/ImportCacheServiceTests.cs`:

```csharp
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Services;

namespace HandsLiftedApp.Tests;

[TestClass]
public class ImportCacheServiceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ImportCacheServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void GetFileImportCacheDirectory_SameContentDifferentAbsolutePath_ReturnsSameDirectory()
    {
        var dirA = Path.Combine(_tempDir, "MachineA_Users_Jeremy");
        var dirB = Path.Combine(_tempDir, "MachineB_VisionScreens_Data");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        var fileA = Path.Combine(dirA, "sermon.pdf");
        var fileB = Path.Combine(dirB, "sermon.pdf");
        File.WriteAllText(fileA, "identical-pdf-bytes");
        File.WriteAllText(fileB, "identical-pdf-bytes");

        var cacheDirA = ImportCacheService.GetFileImportCacheDirectory(fileA);
        var cacheDirB = ImportCacheService.GetFileImportCacheDirectory(fileB);

        Assert.AreEqual(cacheDirA, cacheDirB);
    }

    [TestMethod]
    public void GetFileImportCacheDirectory_DifferentContent_ReturnsDifferentDirectory()
    {
        var fileA = Path.Combine(_tempDir, "sermon-v1.pdf");
        var fileB = Path.Combine(_tempDir, "sermon-v2.pdf");
        File.WriteAllText(fileA, "version-one-bytes");
        File.WriteAllText(fileB, "version-two-bytes");

        var cacheDirA = ImportCacheService.GetFileImportCacheDirectory(fileA);
        var cacheDirB = ImportCacheService.GetFileImportCacheDirectory(fileB);

        Assert.AreNotEqual(cacheDirA, cacheDirB);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ImportCacheServiceTests"`
Expected: FAIL — `GetFileImportCacheDirectory_SameContentDifferentAbsolutePath_ReturnsSameDirectory` fails because today's implementation hashes the path string, so `dirA`'s and `dirB`'s copies hash differently.

- [ ] **Step 3: Rekey by content**

In `HandsLiftedApp.Core/Services/ImportCacheService.cs`, change:

```csharp
    /// <summary>Cache dir for a local file. Stable per absolute path.</summary>
    public static string GetFileImportCacheDirectory(string sourceFilePath)
    {
        var key = Path.GetFullPath(sourceFilePath);
        return GetOrCreateDir(HashKey(key));
    }
```

to:

```csharp
    /// <summary>Cache dir for a local file. Stable per file content, so it survives the source file moving to a different absolute path (e.g. a different machine).</summary>
    public static string GetFileImportCacheDirectory(string sourceFilePath)
    {
        using var stream = File.OpenRead(sourceFilePath);
        var hash = SHA256.HashData(stream);
        return GetOrCreateDir(Convert.ToHexString(hash)[..16]);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~ImportCacheServiceTests"`
Expected: PASS.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 221 tests, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Services/ImportCacheService.cs HandsLiftedApp.Tests/ImportCacheServiceTests.cs
git commit -m "fix: key ImportCacheService by file content instead of absolute path"
```

---

### Task 6: Rewire PDF/PowerPoint/Google Slides `Sync()` to target the cache, not the playlist folder

**Files:**
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PDFSlidesGroupItemInstance.cs:132-141`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs:135-143,176-184`
- Modify: `HandsLiftedApp.Core/Models/RuntimeData/Items/GoogleSlidesGroupItemInstance.cs:138-146`

**Interfaces:**
- Consumes: `ImportCacheService.GetFileImportCacheDirectory(string sourceFilePath)` and `ImportCacheService.GetKeyedCacheDirectory(string keyType, string key)` (Task 5).

No behavior-level automated test: `Sync()` invokes real PDF/PowerPoint conversion (native interop, external processes) that this test project does not exercise anywhere today — the existing test suite has no test for `Sync()` itself. Verified by build succeeding, the full suite staying green, and Task 8's manual end-to-end check (which specifically exercises this path).

- [ ] **Step 1: `PDFSlidesGroupItemInstance.Sync()`**

Change:

```csharp
                            DateTime now = DateTime.Now;
                            string fileName = Path.GetFileName(SourcePresentationFile);

                            string targetDirectory = Path.Join(ParentPlaylist
                                    .PlaylistWorkingDirectory,
                                FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                                now.ToString("yyyy-MM-dd-HH-mm-ss"));
                            Directory.CreateDirectory(targetDirectory);
                            
                            Log.Debug($"Importing PDF file: {SourcePresentationFile}");
```

to:

```csharp
                            string targetDirectory = ImportCacheService.GetFileImportCacheDirectory(SourcePresentationFile);

                            Log.Debug($"Importing PDF file: {SourcePresentationFile}");
```

- [ ] **Step 2: `PowerPointPresentationItemInstance.SyncViaSyncfusion()` and `SyncViaNativeInterop()`**

In both methods, change:

```csharp
                    DateTime now = DateTime.Now;
                    string fileName = Path.GetFileName(SourcePresentationFile);

                    string targetDirectory = Path.Join(ParentPlaylist.PlaylistWorkingDirectory,
                        FilenameUtils.ReplaceInvalidChars(fileName) + "_" +
                        now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    Directory.CreateDirectory(targetDirectory);
```

to:

```csharp
                    string targetDirectory = ImportCacheService.GetFileImportCacheDirectory(SourcePresentationFile);
```

(Both occurrences — the `Log.Debug` lines immediately after stay unchanged.)

- [ ] **Step 3: `GoogleSlidesGroupItemInstance.Sync()`**

Change:

```csharp
                            DateTime now = DateTime.Now;

                            string targetDirectory = Path.Join(ParentPlaylist
                                    .PlaylistWorkingDirectory,
                                FilenameUtils.ReplaceInvalidChars(SourceGooglePresentationId) + "_" +
                                now.ToString("yyyy-MM-dd-HH-mm-ss"));
                            Directory.CreateDirectory(targetDirectory);

                            Log.Debug($"Importing Google Slides Presentation: {SourceGooglePresentationId}");
```

to:

```csharp
                            string targetDirectory = ImportCacheService.GetKeyedCacheDirectory(
                                "GoogleSlidesPresentationId", SourceGooglePresentationId);

                            Log.Debug($"Importing Google Slides Presentation: {SourceGooglePresentationId}");
```

(The `_export` subfolder logic immediately below is unchanged — it's an internal detail of the two-step Google Slides→PDF→PNG conversion, unaffected by where `targetDirectory` itself points.)

- [ ] **Step 4: Build**

Run: `dotnet build HandsLiftedApp.sln --nologo`
Expected: Build succeeds, 0 errors. (Watch for now-unused `FilenameUtils`/`DateTime now` locals flagged as warnings — remove any that the compiler flags as unused in these three files.)

- [ ] **Step 5: Run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 221 tests, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/PDFSlidesGroupItemInstance.cs HandsLiftedApp.Core/Models/RuntimeData/Items/PowerPointPresentationItemInstance.cs HandsLiftedApp.Core/Models/RuntimeData/Items/GoogleSlidesGroupItemInstance.cs
git commit -m "fix: render PDF/PPTX/Google Slides exports into the per-machine cache, not the playlist folder"
```

---

### Task 7: Eager regeneration on playlist load

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/MainViewModel.cs:205-227`
- Test: `HandsLiftedApp.Tests/ItemInstanceFactoryTests.cs` (add a focused test for the extracted helper, not the full async load path — see Step 1)

**Interfaces:**
- Produces: `private static bool NeedsSlideRegeneration(MediaGroupItem group)` (private to `MainViewModel`) — but its logic is simple enough to verify by direct construction in a test that doesn't need `MainViewModel`'s async load machinery; see Step 1.
- Consumes: `IItemSyncable.Sync()` (existing), `MediaGroupItem.Items`/`MediaGroupItem.MediaItem.SourceMediaFilePath` (existing).

The full load path (`Dispatcher.UIThread.InvokeAsync`, file I/O, `MessageBus`) is async UI-thread plumbing this test project doesn't exercise anywhere for `MainViewModel` today. Rather than add a first-of-its-kind harness for that, this task extracts the one piece of new *decision* logic (`NeedsSlideRegeneration`) so it's independently testable, and wires it into the load loop as a one-line call whose correctness follows from the extracted method's own tests plus Task 8's manual end-to-end check.

- [ ] **Step 1: Write the failing test for the extraction target**

Since `NeedsSlideRegeneration` will be `private static` on `MainViewModel`, first write it as a `public static` method on a small, purpose-built static class so it's directly testable — this also keeps `MainViewModel` from accumulating unrelated static helpers (it's already a large file per this codebase's existing-large-file pattern; a new concern gets its own file rather than growing it further).

`HandsLiftedApp.Tests/SlideRegenerationCheckTests.cs`:

```csharp
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Tests;

[TestClass]
public class SlideRegenerationCheckTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlideRegenerationCheckTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void NeedsSlideRegeneration_NoItems_ReturnsTrue()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);

        Assert.IsTrue(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }

    [TestMethod]
    public void NeedsSlideRegeneration_AllBakedFilesExist_ReturnsFalse()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);
        var slidePath = Path.Combine(_tempDir, "slide1.png");
        File.WriteAllText(slidePath, "png-bytes");
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem { SourceMediaFilePath = slidePath });

        Assert.IsFalse(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }

    [TestMethod]
    public void NeedsSlideRegeneration_ABakedFileIsMissing_ReturnsTrue()
    {
        var playlist = new PlaylistInstance();
        var pdfInstance = new PDFSlidesGroupItemInstance(playlist);
        pdfInstance.Items.Add(new MediaGroupItem.MediaItem
        {
            SourceMediaFilePath = Path.Combine(_tempDir, "does-not-exist.png")
        });

        Assert.IsTrue(SlideRegenerationCheck.NeedsSlideRegeneration(pdfInstance));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideRegenerationCheckTests"`
Expected: FAIL — compile error, `SlideRegenerationCheck` doesn't exist yet.

- [ ] **Step 3: Implement `SlideRegenerationCheck`**

`HandsLiftedApp.Core/Models/RuntimeData/Items/SlideRegenerationCheck.cs`:

```csharp
using System.IO;
using System.Linq;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public static class SlideRegenerationCheck
    {
        public static bool NeedsSlideRegeneration(MediaGroupItem group)
        {
            if (group.Items.Count == 0)
            {
                return true;
            }

            return group.Items
                .OfType<MediaGroupItem.MediaItem>()
                .Any(mediaItem => mediaItem.SourceMediaFilePath == null || !File.Exists(mediaItem.SourceMediaFilePath));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~SlideRegenerationCheckTests"`
Expected: PASS — 3 tests.

- [ ] **Step 5: Wire it into the playlist-load loop**

In `HandsLiftedApp.Core/ViewModels/MainViewModel.cs`, immediately after the existing loop that adds each built item into `Playlist.Items` (right after the closing brace of `foreach (var item in builtItems) { await Dispatcher.UIThread.InvokeAsync(() => Playlist.Items.Add(item), DispatcherPriority.Background); }`, and before `Playlist.IsPlaylistLoading = false;`), add:

```csharp
                foreach (var item in builtItems)
                {
                    if (item is IItemSyncable syncable && item is MediaGroupItem group
                        && SlideRegenerationCheck.NeedsSlideRegeneration(group))
                    {
                        syncable.Sync();
                    }
                }
```

(`HandsLiftedApp.Core.Models.RuntimeData` for `IItemSyncable` and `HandsLiftedApp.Data.Models.Items` for `MediaGroupItem` are already imported in this file; add `using HandsLiftedApp.Core.Models.RuntimeData.Items;` for `SlideRegenerationCheck` if not already present — it already is, for the concrete item-instance types.) This only ever matches `PDFSlidesGroupItemInstance`, `PowerPointPresentationItemInstance`, and `GoogleSlidesGroupItemInstance` — the only three types that are both `IItemSyncable` and a `MediaGroupItem`. `Sync()` enqueues onto the existing background worker queue and returns immediately, so this loop does not block the UI thread or delay `IsPlaylistLoading` clearing.

- [ ] **Step 6: Build and run the full suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 224 tests (221 + 3 new), 0 failed.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/RuntimeData/Items/SlideRegenerationCheck.cs HandsLiftedApp.Tests/SlideRegenerationCheckTests.cs HandsLiftedApp.Core/ViewModels/MainViewModel.cs
git commit -m "feat: eagerly regenerate PDF/PPTX/Google Slides exports on playlist load when cache is cold"
```

---

### Task 8: Manual end-to-end verification

**Files:** none (verification only, no code changes)

**Interfaces:** none.

This exercises the parts of the feature that cross UI, native interop, and the filesystem in ways no automated test in this codebase reaches — matching this project's own established precedent (documented in `CLAUDE.md`) that UI-triggered flows must be clicked through in a running app before being considered done.

- [ ] **Step 1: Full regression run**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --nologo`
Expected: PASS — 224 tests, 0 failed.

- [ ] **Step 2: Build and launch the app**

Run: `dotnet build HandsLiftedApp.sln --nologo`, then launch the app and create a new playlist in a fresh folder (e.g. `C:\Temp\PortableTest\`).

- [ ] **Step 3: Exercise every copy-on-add path**

In the running app: drag-and-drop an image and a video onto the playlist from outside `C:\Temp\PortableTest\`; add a PDF and a PPTX via "Add Presentation"; pick a custom theme background graphic (not the default theme) via the theme designer; set a playlist-scoped logo (not the global one) via the logo editor. After each, confirm in Windows Explorer that a copy landed under the expected subfolder (`Media/Images`, `Media/Video`, `Sources`, `Themes/Backgrounds`, `Themes/Logo`) of `C:\Temp\PortableTest\`.

- [ ] **Step 4: Confirm the global default theme/logo are untouched**

Open the theme designer on the shared default theme and the logo editor's "Global" picker — confirm neither their `BackgroundGraphicFilePath` nor `AppPreferences.LogoGraphicFile` were rewritten to point inside `C:\Temp\PortableTest\`.

- [ ] **Step 5: Save, move, and reopen**

Save the playlist. Copy the entire `C:\Temp\PortableTest\` folder to `C:\Temp\PortableTest-Moved\`. Open the copied playlist from its new location. Confirm: every image/video/theme-graphic/logo slide renders (no broken-image placeholders); the PDF and PPTX items show a brief regeneration indicator and then render their slides correctly (first-open-on-this-copy cache miss, per Task 7); reopening the same moved copy a second time is fast (cache hit, per Task 5/6).

- [ ] **Step 6: Confirm no PNGs were shipped in the folder**

In `C:\Temp\PortableTest-Moved\`, confirm there is no rendered-PNG export folder anywhere under it for the PDF/PPTX items (only `Sources/sermon.pdf` etc. — the originals) — the rendered PNGs should exist only under the per-machine `%LocalAppData%\VisionScreens\ImportCache\` directory.

- [ ] **Step 7: Record the result**

If all checks pass, this plan is complete. If any check fails, treat it as a bug against the specific task above (not this task) and fix there before re-running Step 1.
