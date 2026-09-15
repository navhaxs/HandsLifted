# Media Library "Home" Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Home" entry to the Library pane, backed by `AppPreferences.MediaLibraryPath`, and give every `LibraryType.Media` library (Home included) a folder-drill-down thumbnail browser with breadcrumb navigation, replacing today's flat-only Media Bin grid.

**Architecture:** A new `MediaLibraryQueryViewModel`/`MediaLibraryQueryView` pair does non-recursive, on-demand per-folder scanning (folders then files, current-folder-only search) and is routed to by `LibraryViewModel` whenever the selected library's `Config.Type == LibraryType.Media`, via the same "pick a view by ViewModel type" `ContentControl.DataTemplates` mechanism already used for Song/Scripture. Home is a synthetic `Library` instance appended to the same `Libraries` collection library.yml populates, not a special case anywhere else. The now-unreachable flat Media Bin code path is deleted as part of this plan, not left dead.

**Tech Stack:** C# / Avalonia 11 / ReactiveUI, MSTest (existing test project `HandsLiftedApp.Tests`).

**Spec:** `docs/superpowers/specs/2026-09-15-media-library-home-browser-design.md`

## Global Constraints

- Read-only browsing — no folder create/rename/delete from this view.
- Search filters the current folder only, never recursively.
- Folder scan is non-recursive per visited folder (mirrors `Library.Refresh()`'s existing
  top-directory-only pattern), re-run on navigation.
- Routing gate is `SelectedLibrary.Config.Type == LibraryType.Media` — **not** the old
  content-sniffed `Library.isMediaBin`/`LibraryQueryViewModel.IsMediaBin` heuristic, which this
  plan removes. This is an intentional, small behavior change: any `Type=Media` library now always
  gets the new grid+folders view regardless of what's inside it, instead of flipping to a
  list+preview view when its contents happen to look lyric-file-like (`.txt`/`.xml`).
- Home only appears in the library list when `AppPreferences.MediaLibraryPath` is non-empty — no
  placeholder/disabled entry otherwise.
- Adding a file goes through the existing `MessageBus.Current.SendMessage(new
  AddItemByFilePathMessage(...))` → `PlaylistInstance`'s handler → `PortableAssetCopier
  .ResolveOrCopyIntoMediaLibrary` path (already Media-Library-aware from the prior change) — a file
  already under the library is referenced in place, never copied.

---

### Task 1: `MediaLibraryQueryViewModel` — folder scan, breadcrumbs, search

**Files:**
- Create: `HandsLiftedApp.Core/Models/Library/HomeLibraryEntry.cs`
- Create: `HandsLiftedApp.Core/ViewModels/MediaLibraryQueryViewModel.cs`
- Modify: `HandsLiftedApp.Core/Models/Library/Library.cs:75` (visibility only)
- Test: `HandsLiftedApp.Tests/MediaLibraryQueryViewModelTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Core.Models.Library.Library` (existing — `Config.Directory`, `Label`,
  now-`internal` `SupportedMediaExtensions`), `HandsLiftedApp.Comparer.NaturalSortStringComparer`
  (existing, used by `Library.Refresh()` the same way).
- Produces: `HomeLibraryEntry { string FullPath; string Title; bool IsDirectory; }`,
  `BreadcrumbSegment(string Label, string RelativePath)` (both in `HomeLibraryEntry.cs`);
  `MediaLibraryQueryViewModel(Library library)` with `Library Library`, `string RootPath`,
  `string CurrentRelativePath`, `ObservableCollection<HomeLibraryEntry> Entries`,
  `ObservableCollection<BreadcrumbSegment> Breadcrumbs`, `string SearchTerm`,
  `ReactiveCommand<HomeLibraryEntry, Unit> NavigateIntoCommand`,
  `ReactiveCommand<string, Unit> NavigateToBreadcrumbCommand` — all consumed by Task 2 (the view)
  and Task 3 (routing).

- [ ] **Step 1: Bump `Library.SupportedMediaExtensions` from `private` to `internal`**

In `HandsLiftedApp.Core/Models/Library/Library.cs`, change:

```csharp
private static readonly HashSet<string> SupportedMediaExtensions = new(
```

to:

```csharp
internal static readonly HashSet<string> SupportedMediaExtensions = new(
```

This is the exact same image/video/PDF/PowerPoint extension set `Library.Refresh()` already
filters by — `MediaLibraryQueryViewModel` reuses it instead of redeclaring it.

- [ ] **Step 2: Create `HomeLibraryEntry.cs`**

```csharp
namespace HandsLiftedApp.Core.Models.Library
{
    public class HomeLibraryEntry
    {
        public string FullPath { get; init; }
        public string Title { get; init; }
        public bool IsDirectory { get; init; }
    }

    public record BreadcrumbSegment(string Label, string RelativePath);
}
```

- [ ] **Step 3: Write the failing tests**

Create `HandsLiftedApp.Tests/MediaLibraryQueryViewModelTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Tests;

[TestClass]
public class MediaLibraryQueryViewModelTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MediaLibraryQueryViewModelTests_" + Guid.NewGuid().ToString("N"));
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

    private MediaLibraryQueryViewModel CreateVm(string? label = "Home")
    {
        var library = new Library(new LibraryConfig.LibraryDefinition
        {
            Label = label,
            Directory = _tempDir,
            Type = LibraryType.Media
        });
        return new MediaLibraryQueryViewModel(library);
    }

    [TestMethod]
    public void Constructor_ListsSubfoldersBeforeFiles()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Sermons"));
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();

        Assert.AreEqual(2, vm.Entries.Count);
        Assert.IsTrue(vm.Entries[0].IsDirectory);
        Assert.AreEqual("Sermons", vm.Entries[0].Title);
        Assert.IsFalse(vm.Entries[1].IsDirectory);
        Assert.AreEqual("photo.jpg", vm.Entries[1].Title);
    }

    [TestMethod]
    public void Constructor_UnsupportedExtension_Excluded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "notes.docx"), "not media");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();

        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("photo.jpg", vm.Entries[0].Title);
    }

    [TestMethod]
    public void NavigateIntoCommand_UpdatesEntriesAndBreadcrumbs()
    {
        var sermonsDir = Path.Combine(_tempDir, "Sermons");
        Directory.CreateDirectory(sermonsDir);
        File.WriteAllText(Path.Combine(sermonsDir, "sermon1.mp4"), "mp4-bytes");

        var vm = CreateVm();
        var sermonsEntry = vm.Entries.Single(e => e.Title == "Sermons");

        vm.NavigateIntoCommand.Execute(sermonsEntry).Subscribe();

        Assert.AreEqual("Sermons", vm.CurrentRelativePath);
        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("sermon1.mp4", vm.Entries[0].Title);
        Assert.AreEqual(2, vm.Breadcrumbs.Count);
        Assert.AreEqual("Home", vm.Breadcrumbs[0].Label);
        Assert.AreEqual("", vm.Breadcrumbs[0].RelativePath);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].Label);
        Assert.AreEqual("Sermons", vm.Breadcrumbs[1].RelativePath);
    }

    [TestMethod]
    public void NavigateToBreadcrumbCommand_JumpsBackToRoot()
    {
        var sermonsDir = Path.Combine(_tempDir, "Sermons");
        Directory.CreateDirectory(sermonsDir);
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.NavigateIntoCommand.Execute(vm.Entries.Single(e => e.Title == "Sermons")).Subscribe();

        vm.NavigateToBreadcrumbCommand.Execute("").Subscribe();

        Assert.AreEqual("", vm.CurrentRelativePath);
        Assert.AreEqual(2, vm.Entries.Count);
        Assert.AreEqual(1, vm.Breadcrumbs.Count);
    }

    [TestMethod]
    public void SearchTerm_FiltersCurrentFolderOnly_NotRecursively()
    {
        var subDir = Path.Combine(_tempDir, "Sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_tempDir, "apple.jpg"), "jpg-bytes");
        File.WriteAllText(Path.Combine(subDir, "banana.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.SearchTerm = "banana";

        Assert.AreEqual(0, vm.Entries.Count,
            "banana.jpg lives in Sub, not the root folder currently being viewed — search must not recurse into it.");
    }

    [TestMethod]
    public void SearchTerm_MatchesFolderNamesToo()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Sermons"));
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "jpg-bytes");

        var vm = CreateVm();
        vm.SearchTerm = "serm";

        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("Sermons", vm.Entries[0].Title);
    }

    [TestMethod]
    public void MissingDirectory_ReturnsEmptyEntries_NoException()
    {
        var library = new Library(new LibraryConfig.LibraryDefinition
        {
            Label = "Home",
            Directory = Path.Combine(_tempDir, "DoesNotExist"),
            Type = LibraryType.Media
        });

        var vm = new MediaLibraryQueryViewModel(library);

        Assert.AreEqual(0, vm.Entries.Count);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter MediaLibraryQueryViewModelTests`
Expected: FAIL to compile — `MediaLibraryQueryViewModel` doesn't exist yet.

- [ ] **Step 5: Implement `MediaLibraryQueryViewModel`**

Create `HandsLiftedApp.Core/ViewModels/MediaLibraryQueryViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Core.Models.Library;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels
{
    public class MediaLibraryQueryViewModel : ReactiveObject
    {
        public Library Library { get; }
        public string RootPath => Library.Config.Directory;

        private string _currentRelativePath = "";
        public string CurrentRelativePath
        {
            get => _currentRelativePath;
            private set => this.RaiseAndSetIfChanged(ref _currentRelativePath, value);
        }

        private string _searchTerm = "";
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchTerm, value);
                ApplyFilter();
            }
        }

        public ObservableCollection<HomeLibraryEntry> Entries { get; } = new();
        public ObservableCollection<BreadcrumbSegment> Breadcrumbs { get; } = new();

        public ReactiveCommand<HomeLibraryEntry, Unit> NavigateIntoCommand { get; }
        public ReactiveCommand<string, Unit> NavigateToBreadcrumbCommand { get; }

        private List<HomeLibraryEntry> _allEntriesInCurrentFolder = new();

        public MediaLibraryQueryViewModel(Library library)
        {
            Library = library;

            NavigateIntoCommand = ReactiveCommand.Create<HomeLibraryEntry>(entry =>
            {
                if (entry.IsDirectory)
                {
                    Navigate(string.IsNullOrEmpty(CurrentRelativePath)
                        ? entry.Title
                        : Path.Combine(CurrentRelativePath, entry.Title));
                }
            });

            NavigateToBreadcrumbCommand = ReactiveCommand.Create<string>(Navigate);

            Navigate("");
        }

        private void Navigate(string relativePath)
        {
            CurrentRelativePath = relativePath ?? "";
            RescanCurrentFolder();
            RebuildBreadcrumbs();
        }

        private void RescanCurrentFolder()
        {
            var currentDir = string.IsNullOrEmpty(CurrentRelativePath)
                ? RootPath
                : Path.Combine(RootPath, CurrentRelativePath);

            var entries = new List<HomeLibraryEntry>();

            try
            {
                if (Directory.Exists(currentDir))
                {
                    var comparer = new NaturalSortStringComparer(StringComparison.Ordinal);

                    var folders = new DirectoryInfo(currentDir).GetDirectories()
                        .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                        .OrderBy(d => d.Name, comparer)
                        .Select(d => new HomeLibraryEntry { FullPath = d.FullName, Title = d.Name, IsDirectory = true });

                    var files = new DirectoryInfo(currentDir).GetFiles()
                        .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                        .Where(f => Library.SupportedMediaExtensions.Contains(f.Extension.TrimStart('.')))
                        .OrderBy(f => f.Name, comparer)
                        .Select(f => new HomeLibraryEntry { FullPath = f.FullName, Title = f.Name, IsDirectory = false });

                    entries.AddRange(folders);
                    entries.AddRange(files);
                }
                else
                {
                    Log.Warning("Media library folder [{Directory}] does not exist", currentDir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan media library folder [{Directory}]", currentDir);
            }

            _allEntriesInCurrentFolder = entries;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<HomeLibraryEntry> filtered = string.IsNullOrWhiteSpace(SearchTerm)
                ? _allEntriesInCurrentFolder
                : _allEntriesInCurrentFolder.Where(e =>
                    e.Title.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));

            Entries.Clear();
            foreach (var entry in filtered)
            {
                Entries.Add(entry);
            }
        }

        private void RebuildBreadcrumbs()
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbSegment(Library.Label, ""));

            if (string.IsNullOrEmpty(CurrentRelativePath))
            {
                return;
            }

            var segments = CurrentRelativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var accumulated = "";
            foreach (var segment in segments)
            {
                accumulated = string.IsNullOrEmpty(accumulated) ? segment : Path.Combine(accumulated, segment);
                Breadcrumbs.Add(new BreadcrumbSegment(segment, accumulated));
            }
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter MediaLibraryQueryViewModelTests`
Expected: PASS, 7 tests.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Models/Library/Library.cs HandsLiftedApp.Core/Models/Library/HomeLibraryEntry.cs HandsLiftedApp.Core/ViewModels/MediaLibraryQueryViewModel.cs HandsLiftedApp.Tests/MediaLibraryQueryViewModelTests.cs
git commit -m "feat: add MediaLibraryQueryViewModel for folder-drill-down media browsing"
```

---

### Task 2: `MediaLibraryQueryView` — breadcrumb bar + folder/file thumbnail grid

**Files:**
- Create: `HandsLiftedApp.Core/Views/LibraryView/MediaLibraryQueryView.axaml`
- Create: `HandsLiftedApp.Core/Views/LibraryView/MediaLibraryQueryView.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/LibraryView/LibraryPaneView.axaml:86-93`

**Interfaces:**
- Consumes: `MediaLibraryQueryViewModel` (Task 1) — `Entries`, `Breadcrumbs`, `SearchTerm`,
  `NavigateIntoCommand`, `NavigateToBreadcrumbCommand`; `HomeLibraryEntry.FullPath/Title/IsDirectory`;
  `HandsLiftedApp.Models.PlaylistActions.AddItemByFilePathMessage` (existing — note its namespace
  doesn't match its `HandsLiftedApp.Controls\Messages\` file path).
- Produces: `MediaLibraryQueryView` (a `UserControl`), wired into `LibraryPaneView.axaml`'s
  `ContentControl.DataTemplates` so Task 3's routing has somewhere to render.

This task has no automated test — it's Avalonia XAML/UI, exercised manually per the checklist at
the end of this plan. Build succeeding is the only automated signal.

- [ ] **Step 1: Create `MediaLibraryQueryView.axaml`**

```xml
<UserControl
    d:DesignHeight="350"
    d:DesignWidth="600"
    mc:Ignorable="d"
    x:Class="HandsLiftedApp.Core.Views.LibraryView.MediaLibraryQueryView"
    x:DataType="viewModels:MediaLibraryQueryViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:asyncImageLoader="clr-namespace:AsyncImageLoader;assembly=AsyncImageLoader.Avalonia"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:material="using:Material.Icons.Avalonia"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:viewModels="clr-namespace:HandsLiftedApp.Core.ViewModels"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Design.DataContext>
        <viewModels:MediaLibraryQueryViewModel />
    </Design.DataContext>

    <DockPanel>
        <TextBox
            DockPanel.Dock="Top"
            KeyDown="InputElement_OnKeyDown"
            Text="{Binding SearchTerm}"
            Watermark="Filter"
            x:Name="SearchBox" />

        <ItemsControl
            DockPanel.Dock="Top"
            ItemsSource="{Binding Breadcrumbs}"
            Margin="0,6">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <StackPanel Orientation="Horizontal" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="{Binding Label}" Click="Breadcrumb_OnClick" Padding="4,0" />
                        <TextBlock Text="/" VerticalAlignment="Center" Margin="2,0" />
                    </StackPanel>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <ListBox
            HorizontalAlignment="Stretch"
            ItemsSource="{Binding Entries}"
            ScrollViewer.HorizontalScrollBarVisibility="Disabled">
            <ListBox.ItemsPanel>
                <ItemsPanelTemplate>
                    <WrapPanel ItemHeight="120" ItemWidth="200" />
                </ItemsPanelTemplate>
            </ListBox.ItemsPanel>
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <DockPanel
                        DoubleTapped="Entry_OnDoubleTapped"
                        PointerPressed="Entry_OnPointerPressed">
                        <DockPanel.ContextMenu>
                            <ContextMenu>
                                <MenuItem
                                    Click="AddToPlaylist_OnClick"
                                    Header="Add to playlist"
                                    IsVisible="{Binding !IsDirectory}" />
                            </ContextMenu>
                        </DockPanel.ContextMenu>
                        <TextBlock DockPanel.Dock="Bottom" Text="{Binding Title}" />
                        <Viewbox>
                            <Grid Background="Black" Height="720" Width="1280">

                                <!--  Folder tile  -->
                                <material:MaterialIcon
                                    IsVisible="{Binding IsDirectory}"
                                    Foreground="#CCCCCC"
                                    Height="420"
                                    HorizontalAlignment="Center"
                                    Kind="FolderOutline"
                                    VerticalAlignment="Center"
                                    Width="420" />

                                <!--  File thumbnail  -->
                                <Image
                                    x:Name="ThumbImage"
                                    IsVisible="{Binding !IsDirectory}"
                                    HorizontalAlignment="Stretch"
                                    RenderOptions.BitmapInterpolationMode="HighQuality"
                                    VerticalAlignment="Stretch"
                                    asyncImageLoader:ImageLoader.Source="{Binding FullPath}" />

                                <StackPanel
                                    HorizontalAlignment="Center"
                                    Spacing="16"
                                    VerticalAlignment="Center">
                                    <StackPanel.IsVisible>
                                        <MultiBinding Converter="{x:Static BoolConverters.And}">
                                            <Binding Path="!IsDirectory" />
                                            <Binding
                                                Converter="{x:Static BoolConverters.Not}"
                                                ElementName="ThumbImage"
                                                Path="(asyncImageLoader:ImageLoader.IsLoading)" />
                                            <Binding
                                                Converter="{x:Static ObjectConverters.IsNull}"
                                                ElementName="ThumbImage"
                                                Path="Source" />
                                        </MultiBinding>
                                    </StackPanel.IsVisible>
                                    <material:MaterialIcon
                                        Foreground="#666666"
                                        Height="128"
                                        HorizontalAlignment="Center"
                                        Kind="ImageOffOutline"
                                        Width="128" />
                                    <TextBlock
                                        FontSize="36"
                                        Foreground="#888888"
                                        HorizontalAlignment="Center"
                                        Text="No preview available" />
                                </StackPanel>

                                <StackPanel
                                    HorizontalAlignment="Center"
                                    IsVisible="{Binding #ThumbImage.(asyncImageLoader:ImageLoader.IsLoading)}"
                                    Spacing="16"
                                    VerticalAlignment="Center">
                                    <ProgressBar
                                        Height="96"
                                        IsIndeterminate="True"
                                        Width="200" />
                                </StackPanel>
                            </Grid>
                        </Viewbox>
                    </DockPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </DockPanel>

</UserControl>
```

- [ ] **Step 2: Create `MediaLibraryQueryView.axaml.cs`**

```csharp
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.LibraryView
{
    public partial class MediaLibraryQueryView : UserControl
    {
        private const double DragThreshold = 4.0;
        private Point? _dragStart;
        private HomeLibraryEntry? _pendingDragItem;
        private Control? _pendingDragControl;
        private PointerPressedEventArgs? _pendingDragPressedArgs;

        public MediaLibraryQueryView()
        {
            InitializeComponent();
        }

        private MediaLibraryQueryViewModel? Vm => DataContext as MediaLibraryQueryViewModel;

        private static HomeLibraryEntry? GetEntry(object? sender) =>
            sender is Control { DataContext: HomeLibraryEntry entry } ? entry : null;

        private void Entry_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry is { IsDirectory: true })
            {
                Vm?.NavigateIntoCommand.Execute(entry).Subscribe();
            }
        }

        private void Breadcrumb_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: BreadcrumbSegment segment })
            {
                Vm?.NavigateToBreadcrumbCommand.Execute(segment.RelativePath).Subscribe();
            }
        }

        private void AddToPlaylist_OnClick(object? sender, RoutedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry == null || entry.IsDirectory) return;

            MessageBus.Current.SendMessage(new AddItemByFilePathMessage(new List<string> { entry.FullPath }));
        }

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
            }
        }

        // Drag-to-add for file tiles only, mirroring LibraryQueryView's Media Bin drag behavior.
        private void Entry_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var entry = GetEntry(sender);
            if (entry is not { IsDirectory: false } || sender is not Control control) return;

            _dragStart = e.GetPosition(null);
            _pendingDragItem = entry;
            _pendingDragControl = control;
            _pendingDragPressedArgs = e;
            control.PointerMoved += Entry_OnPointerMoved;
            control.PointerReleased += Entry_OnPointerReleased;
            e.Pointer.Capture(control);
        }

        private void Entry_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            CancelPendingDrag();
        }

        private void CancelPendingDrag()
        {
            if (_pendingDragControl != null)
            {
                _pendingDragControl.PointerMoved -= Entry_OnPointerMoved;
                _pendingDragControl.PointerReleased -= Entry_OnPointerReleased;
            }
            _dragStart = null;
            _pendingDragItem = null;
            _pendingDragControl = null;
            _pendingDragPressedArgs = null;
        }

        private async void Entry_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStart == null || _pendingDragItem == null || _pendingDragPressedArgs == null) return;

            var delta = e.GetPosition(null) - _dragStart.Value;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

            var item = _pendingDragItem;
            var pressedArgs = _pendingDragPressedArgs;
            CancelPendingDrag();

            var dragData = new DataTransfer();
            var topLevel = TopLevel.GetTopLevel(this);
            IStorageFile file = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FullPath));
            dragData.Add(DataTransferItem.Create(DataFormat.File, file));

            await DragDrop.DoDragDropAsync(pressedArgs, dragData, DragDropEffects.Copy);
        }
    }
}
```

- [ ] **Step 3: Register the view in `LibraryPaneView.axaml`**

In `HandsLiftedApp.Core/Views/LibraryView/LibraryPaneView.axaml`, change:

```xml
<ContentControl.DataTemplates>
    <DataTemplate DataType="viewModels:LibraryQueryViewModel">
        <library:LibraryQueryView />
    </DataTemplate>
</ContentControl.DataTemplates>
```

to:

```xml
<ContentControl.DataTemplates>
    <DataTemplate DataType="viewModels:LibraryQueryViewModel">
        <library:LibraryQueryView />
    </DataTemplate>
    <DataTemplate DataType="viewModels:MediaLibraryQueryViewModel">
        <library:MediaLibraryQueryView />
    </DataTemplate>
</ContentControl.DataTemplates>
```

- [ ] **Step 4: Build to verify no compile/XAML errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug`
Expected: Build succeeded, no new errors (existing pre-existing warnings are fine).

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Views/LibraryView/MediaLibraryQueryView.axaml HandsLiftedApp.Core/Views/LibraryView/MediaLibraryQueryView.axaml.cs HandsLiftedApp.Core/Views/LibraryView/LibraryPaneView.axaml
git commit -m "feat: add MediaLibraryQueryView (breadcrumb + folder/file grid)"
```

---

### Task 3: Route `LibraryType.Media` to the new view; add synthetic "Home" library

**Files:**
- Modify: `HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs:68-135` (`ReloadLibraries`), `164-174`
  (`SelectedLibrary` subscription), `219-225` (`ActiveQuery` property)

**Interfaces:**
- Consumes: `MediaLibraryQueryViewModel` (Task 1), `LibraryQueryViewModel` (existing),
  `AppPreferencesViewModel.MediaLibraryPath` (existing, from the prior Media Library change),
  `Globals.Instance.AppPreferences` (existing).
- Produces: `LibraryViewModel.ActiveQuery` retyped `object?` — consumed by `LibraryPaneView.axaml`'s
  `ContentControl.Content` binding (already wired in Task 2).

No new automated test for this task — `LibraryViewModel.ReloadLibraries()` reads/writes a real
config file on disk and constructs real `Library`/`SongLibrary` instances that do real directory
I/O; it has no existing test coverage for the same reason, and adding a harness for it is out of
scope for this plan. Verified via the manual checklist at the end of this plan.

- [ ] **Step 1: Retype `ActiveQuery` to `object?`**

In `LibraryViewModel.cs`, change:

```csharp
private LibraryQueryViewModel? _activeQuery;

public LibraryQueryViewModel? ActiveQuery
{
    get => _activeQuery;
    set => this.RaiseAndSetIfChanged(ref _activeQuery, value);
}
```

to:

```csharp
private object? _activeQuery;

public object? ActiveQuery
{
    get => _activeQuery;
    set => this.RaiseAndSetIfChanged(ref _activeQuery, value);
}
```

- [ ] **Step 2: Branch the `SelectedLibrary` subscription by `Config.Type`**

Change:

```csharp
this.WhenAnyValue(t => t.SelectedLibrary).Subscribe(x =>
{
    if (x == null)
    {
        ActiveQuery = null;
    }
    else
    {
        ActiveQuery = new LibraryQueryViewModel(new List<Library>(){x});
    }                
});
```

to:

```csharp
this.WhenAnyValue(t => t.SelectedLibrary).Subscribe(x =>
{
    if (x == null)
    {
        ActiveQuery = null;
    }
    else if (x.Config.Type == LibraryType.Media)
    {
        ActiveQuery = new MediaLibraryQueryViewModel(x);
    }
    else
    {
        ActiveQuery = new LibraryQueryViewModel(new List<Library>(){x});
    }                
});
```

- [ ] **Step 3: Append the synthetic "Home" library in `ReloadLibraries()`**

At the end of `ReloadLibraries()`, after the existing `foreach (var lib in built) { Libraries.Add(lib); }` loop, add:

```csharp
var mediaLibraryPath = Globals.Instance.AppPreferences?.MediaLibraryPath;
if (!string.IsNullOrWhiteSpace(mediaLibraryPath))
{
    Libraries.Add(new Library(new LibraryConfig.LibraryDefinition
    {
        Label = "Home",
        Directory = mediaLibraryPath,
        Type = LibraryType.Media
    }));
}
```

- [ ] **Step 4: Build to verify no compile errors**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug`
Expected: Build succeeded.

- [ ] **Step 5: Run the full test suite to verify nothing broke**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj -c Debug --no-restore`
Expected: All tests pass (same count as before this task, plus Task 1's 7 new ones).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/ViewModels/LibraryViewModel.cs
git commit -m "feat: route Media-type libraries to the folder browser; add synthetic Home library"
```

---

### Task 4: Remove the now-dead flat Media Bin code path

Once Task 3 routes every `LibraryType.Media` library to `MediaLibraryQueryViewModel`,
`LibraryQueryViewModel` is only ever constructed for `Song`/`Scripture` libraries. Its `IsMediaBin`
branch (driven by content-sniffing `.txt`/`.xml`) and `Library.isMediaBin` (driven by the same
sniffing inside `Refresh()`) can no longer be reached with `IsMediaBin == true` in practice — Song
libraries' `SongItemReference`/song source files and Scripture libraries' data are never bare image/
video/PDF/PPTX files, so `IsMediaBin` (all-results-are-non-lyric-files) is always `false` for them.
This task deletes the dead branch instead of leaving it unreachable.

**Files:**
- Modify: `HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml:30-110`
- Modify: `HandsLiftedApp.Core/ViewModels/LibraryQueryViewModel.cs:22-26, 86-87, 118-122`
- Modify: `HandsLiftedApp.Core/Models/Library/Library.cs:33-39, 104-105`
- Modify: `HandsLiftedApp.Core/Models/Library/SongLibrary.cs:31`
- Modify: `HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs:16`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new — pure removal. Confirm no other reader of `Library.isMediaBin` /
  `LibraryQueryViewModel.IsMediaBin` exists before deleting (a fresh grep, since other tasks in this
  plan don't add new readers, but always verify against the actual tree rather than this plan's
  point-in-time snapshot).

- [ ] **Step 1: Grep for any other reader of the two properties**

Run: `git grep -n "IsMediaBin\|isMediaBin"` (from the repo root)
Expected: Only the six locations listed in "Files" above. If anything else turns up, stop and
reconsider before deleting — do not blindly delete a property something else still reads.

- [ ] **Step 2: Remove the flat Media Bin `ListBox` from `LibraryQueryView.axaml`**

Delete the whole `<!-- Media Bin -->` `ListBox` block (`LibraryQueryView.axaml:30-107` in the
pre-Task-4 file — the block whose root is `<ListBox HorizontalAlignment="Stretch"
IsVisible="{Binding IsMediaBin}" ...>` through its closing `</ListBox>`), and remove
`IsVisible="{Binding !IsMediaBin}"` from the remaining `<Grid ColumnDefinitions="2*,1,3*" ...>`
(the Song Library grid) since it's now the only content of that `<Grid>` wrapper — the `Grid`
element itself can stay, just drop the now-meaningless `IsVisible` binding so it's simply always
visible.

- [ ] **Step 3: Remove `IsMediaBin` from `LibraryQueryViewModel.cs`**

Delete:

```csharp
private ObservableAsPropertyHelper<bool> _isMediaBin;
public bool IsMediaBin
{
    get => _isMediaBin.Value;
}
```

and, in the parameterless design-time constructor, delete:

```csharp
_isMediaBin = Observable.Return(false)
    .ToProperty(this, x => x.IsMediaBin);
```

and, in the real constructor, delete:

```csharp
_isMediaBin = this
    .WhenAnyValue(x => x.SearchResults)
    .Select(x => x?.All(result => !IsLyricFile(result.FullFilePath)) ?? false)
    .ObserveOn(RxSchedulers.MainThreadScheduler)
    .ToProperty(this, x => x.IsMediaBin);
```

`IsLyricFile` itself (`LibraryQueryViewModel.cs:68-72`) is now unused too — delete it as well.

- [ ] **Step 4: Remove `isMediaBin` from `Library.cs`**

Delete:

```csharp
private bool _isMediaBin;

public bool isMediaBin
{
    get => _isMediaBin;
    set => this.RaiseAndSetIfChanged(ref _isMediaBin, value);
}
```

and, inside `Refresh()`, delete:

```csharp
isMediaBin = !(Items.Count > 0 && (Items.First().FullFilePath.ToLower().EndsWith("txt") ||
                                   Items.First().FullFilePath.ToLower().EndsWith("xml")));
```

- [ ] **Step 5: Remove the now-pointless assignments in `SongLibrary.cs` and `ScriptureLibrary.cs`**

In `SongLibrary.cs`'s constructor, delete the line `isMediaBin = false;`.
In `ScriptureLibrary.cs`'s constructor, delete the line `isMediaBin = false;`.

- [ ] **Step 6: Build and run the full test suite**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj -c Debug`
Expected: Build succeeded, no errors.

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj -c Debug --no-restore`
Expected: All tests pass, same count as after Task 3.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml HandsLiftedApp.Core/ViewModels/LibraryQueryViewModel.cs HandsLiftedApp.Core/Models/Library/Library.cs HandsLiftedApp.Core/Models/Library/SongLibrary.cs HandsLiftedApp.Core/Models/Library/ScriptureLibrary.cs
git commit -m "refactor: remove dead flat Media Bin view (superseded by MediaLibraryQueryView)"
```

---

## Manual Verification Checklist

No Avalonia UI test harness exists in this repo — run the app and check by hand:

1. With `AppPreferences.MediaLibraryPath` unset, open the Library pane: no "Home" entry appears.
2. Set a Media Library folder in Setup containing a mix of subfolders and image/video files. Open
   the Library pane: a "Home" entry now appears in the library list.
3. Select "Home": the grid shows subfolders (folder icon) before files (thumbnails), current folder
   only.
4. Double-click a subfolder: it drills in: entries update to that subfolder's contents, and the
   breadcrumb bar gains a segment.
5. Click an earlier breadcrumb segment: navigates back to that level, entries update accordingly.
6. Type into the filter box while inside a subfolder that has its own nested subfolder containing a
   uniquely-named file: confirm that file does NOT show up in results (search doesn't recurse).
7. Drag a file tile onto the playlist: it's added, and since it's already under the Media Library,
   confirm (e.g. via a log line or by checking the saved playlist XML) it was referenced in place,
   not copied.
8. Right-click a file tile → "Add to playlist": same result as dragging.
9. Select an existing `library.yml`-configured `Type: Media` library (if one exists in your test
   config) that has subfolders on disk: confirm it now also shows the folder browser with working
   drill-down, not the old flat grid.
10. Select a `Song` or `Scripture` library: confirm it's unchanged — still the list + preview pane,
    no breadcrumb bar.
