using System.Collections;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core;

namespace HandsLiftedApp.DevTools;

/// <summary>Curated "what does the app think is going on" snapshot, for asserting against without parsing pixels.</summary>
public static class AppState
{
    public static object Snapshot()
    {
        var vm = Globals.Instance.MainViewModel;
        var pl = vm?.Playlist;

        object? playlist = null;
        if (pl != null)
        {
            var items = pl.Items.Select((item, i) => new
            {
                index = i,
                type = item.GetType().Name,
                title = item.Title,
                uuid = item.UUID,
                slides = CountOf(item, "Slides") ?? CountOf(item, "Items"),
            }).ToList();

            playlist = new
            {
                title = pl.Title,
                file = pl.PlaylistFilePath,
                dirty = pl.IsDirty,
                presentationState = pl.PresentationState.ToString(),
                selectedItem = pl.SelectedItem is { } si ? new { type = si.GetType().Name, title = si.Title, uuid = si.UUID } : null,
                activeSlide = pl.ActiveSlide is { } s ? s.GetType().Name : null,
                itemCount = items.Count,
                items,
                undo = new
                {
                    canUndo = pl.UndoStack.CanUndo,
                    undo = pl.UndoStack.UndoDescription,
                    canRedo = pl.UndoStack.CanRedo,
                    redo = pl.UndoStack.RedoDescription,
                },
            };
        }

        return new
        {
            started = vm != null,
            playlist,
            windows = Surfaces.Windows().Select(w => new
            {
                id = NodeRegistry.IdFor(w),
                type = w.GetType().Name,
                title = w.Title,
                visible = w.IsVisible,
                active = w.IsActive,
                state = w.WindowState.ToString(),
            }).ToList(),
            openPopups = Surfaces.OpenPopups().Count,
        };
    }

    private static int? CountOf(object o, string property)
    {
        try
        {
            var p = o.GetType().GetProperty(property);
            return p?.GetValue(o) is ICollection c ? c.Count : null;
        }
        catch { return null; }
    }
}
