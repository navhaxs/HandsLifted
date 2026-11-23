using System.Runtime.CompilerServices;
using Avalonia;

namespace HandsLiftedApp.DevTools;

/// <summary>
/// Hands out small integer ids for visuals so a client can say "click node 42" after seeing it in /tree.
/// Ids are stable for the life of the visual and never reused. UI thread only.
/// </summary>
internal static class NodeRegistry
{
    private static int _next = 1;
    private static readonly Dictionary<int, WeakReference<Visual>> ById = new();
    private static readonly ConditionalWeakTable<Visual, object> IdOf = new();

    public static int IdFor(Visual visual)
    {
        if (IdOf.TryGetValue(visual, out var existing)) return (int)existing;
        var id = _next++;
        IdOf.Add(visual, id);
        ById[id] = new WeakReference<Visual>(visual);
        return id;
    }

    public static Visual? Get(int id) => ById.TryGetValue(id, out var weak) && weak.TryGetTarget(out var v) ? v : null;
}
