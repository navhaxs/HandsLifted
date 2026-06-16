# Add New Song to Library — Design Spec

**Date:** 2026-06-16  
**Status:** Approved

---

## Summary

Wire up the existing "Add new song" button in `LibraryQueryView` to open `SongEditorWindow` in a "create new song" mode. Songs only write to disk when the user explicitly clicks **Save**. A confirm dialog appears if the user closes the window with unsaved edits.

---

## Architecture

### New song mode vs. edit mode

`SongEditorViewModel` gains a `SongLibrary?` property (init-only). When non-null, the window is in **new song mode**:

- `IsNewSongMode => SongLibrary != null` — drives UI visibility
- Save/Discard buttons appear
- On close with unsaved edits → confirm dialog

When `SongLibrary` is null, behaviour is unchanged (existing edit/add-to-playlist flow).

---

## File Changes

### 1. `HandsLiftedApp.Core/Models/Library/Library.cs`

Add `public void TriggerRefresh() => Refresh();`  
Exposes the protected `Refresh()` so callers can reload the library item list after saving a new song.

### 2. `HandsLiftedApp.Core/ViewModels/Editor/SongEditorViewModel.cs`

Add:
```csharp
public SongLibrary? SongLibrary { get; init; }
public bool IsNewSongMode => SongLibrary != null;
```

No reactive wrapper needed — `SongLibrary` is set once at construction.

### 3. `HandsLiftedApp.Core/ViewModels/LibraryQueryViewModel.cs`

Add:
```csharp
public SongLibrary? ActiveSongLibrary =>
    _libraries.OfType<SongLibrary>().FirstOrDefault();
```

Gives `LibraryQueryView` access to the library object without exposing the full list.

### 4. `HandsLiftedApp.Core/Views/LibraryView/LibraryQueryView.axaml.cs`

Implement `AddItem_OnClick`:

```csharp
private void AddItem_OnClick(object? sender, RoutedEventArgs e)
{
    if (DataContext is not LibraryQueryViewModel vm) return;
    var songLibrary = vm.ActiveSongLibrary;
    if (songLibrary == null) return;

    var playlist = Globals.Instance.MainViewModel.Playlist;
    var editorVm = new SongEditorViewModel(new SongItemInstance(null), playlist)
    {
        SongLibrary = songLibrary
    };
    var editor = new SongEditorWindow { DataContext = editorVm };
    editor.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    editor.Show();
}
```

### 5. `HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml`

Uncomment the **Save** and **Discard** buttons inside the right-side `StackPanel`. Wrap the `StackPanel` (or the buttons themselves) with `IsVisible="{Binding IsNewSongMode}"`. Add `Click` attributes:

- Save: `Click="SaveToLibrary_OnClick"`
- Discard: `Click="Discard_OnClick"`

The "Save to Library" button (first commented block, wrong click handler) stays commented.

### 6. `HandsLiftedApp.Core/Views/Editors/SongEditorWindow.axaml.cs`

Add:

```csharp
private bool _closeConfirmed = false;

private void Discard_OnClick(object? sender, RoutedEventArgs e)
{
    _closeConfirmed = true;
    Close();
}

private void SaveToLibrary_OnClick(object? sender, RoutedEventArgs e)
{
    if (DataContext is SongEditorViewModel vm)
        DoSaveToLibrary(vm);
}

private void DoSaveToLibrary(SongEditorViewModel vm)
{
    // serialize SongItemInstance → SongItem → XML
    // filename = sanitized title + ".xml", fallback "Untitled.xml"
    // write to vm.SongLibrary.Config.Directory
    // vm.SongLibrary.TriggerRefresh()
    _closeConfirmed = true;
    Close();
}

protected override async void OnClosing(WindowClosingEventArgs e)
{
    base.OnClosing(e);
    if (_closeConfirmed) return;
    if (DataContext is SongEditorViewModel { IsNewSongMode: true } vm)
    {
        bool hasEdits = !string.IsNullOrEmpty(vm.Song.Title) || vm.Song.Stanzas.Count > 0;
        if (!hasEdits) return;

        e.Cancel = true;

        var dialog = new NewSongUnsavedConfirmationWindow();
        await dialog.ShowDialog(this);

        switch (dialog.Result)
        {
            case NewSongUnsavedConfirmationWindow.DialogResult.Save:
                DoSaveToLibrary(vm);
                break;
            case NewSongUnsavedConfirmationWindow.DialogResult.Discard:
                _closeConfirmed = true;
                Close();
                break;
            // Cancel → do nothing, window stays open
        }
    }
}
```

### 7. New: `HandsLiftedApp.Core/Views/Confirmation/NewSongUnsavedConfirmationWindow.axaml` + `.cs`

Modelled on existing `UnsavedChangesConfirmationWindow`. Buttons: **Save**, **Don't Save**, **Cancel**.  
Text: "Save this new song?" / "The song has not been saved to the library yet."

---

## Edge Cases

| Case | Behaviour |
|---|---|
| Empty title on Save | Filename = `Untitled.xml` |
| Title contains invalid path chars | Strip chars before building filename |
| File collision (`Untitled.xml` exists) | Overwrite |
| Library directory doesn't exist | Log error, do not close |
| Non-song library selected | `ActiveSongLibrary` returns null, button does nothing |

---

## What Does Not Change

- Existing "edit song" flow (`SongLibrary = null`) — no change
- Existing "add to playlist" flow (`ItemInsertIndex` — no change
- `UnsavedChangesConfirmationWindow` — untouched
- "Save to Library" button (first commented block) — stays commented
