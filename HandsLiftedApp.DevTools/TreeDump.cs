using System.Text;
using Avalonia;
using Avalonia.VisualTree;

namespace HandsLiftedApp.DevTools;

public sealed class TreeOptions
{
    public int MaxDepth { get; init; } = 10;
    public bool IncludeHidden { get; init; }
    /// <summary>false = elide pure layout containers (the default compact view).</summary>
    public bool Everything { get; init; }
    public int MaxNodes { get; init; } = 1500;
}

public sealed class TreeNode
{
    public required Visual Visual { get; init; }
    public List<TreeNode> Children { get; } = [];
}

public static class TreeDump
{
    public static (List<TreeNode> Roots, int Count, bool Truncated) Build(Visual root, TreeOptions o)
    {
        var count = 0;
        var truncated = false;

        List<TreeNode> Walk(Visual v, int depth)
        {
            if (count >= o.MaxNodes) { truncated = true; return []; }
            if (!o.IncludeHidden && !v.IsVisible) return [];

            var show = o.Everything || NodeInfo.IsInteresting(v) || v == root;
            if (show && depth > o.MaxDepth) return [];

            var node = show ? new TreeNode { Visual = v } : null;
            if (node != null) count++;

            var kids = new List<TreeNode>();
            foreach (var child in v.GetVisualChildren())
                kids.AddRange(Walk(child, show ? depth + 1 : depth));

            if (node == null) return kids;
            node.Children.AddRange(kids);
            return [node];
        }

        return (Walk(root, 0), count, truncated);
    }

    public static string ToText(IEnumerable<TreeNode> roots)
    {
        var sb = new StringBuilder();
        void Emit(TreeNode n, int indent, string? parentDc)
        {
            sb.Append(' ', indent * 2).AppendLine(NodeInfo.Line(n.Visual, parentDc));
            var dc = (n.Visual as StyledElement)?.DataContext?.GetType().Name ?? parentDc;
            foreach (var c in n.Children) Emit(c, indent + 1, dc);
        }
        foreach (var r in roots) Emit(r, 0, null);
        return sb.ToString();
    }

    public static object ToJson(TreeNode n)
    {
        var d = NodeInfo.Describe(n.Visual);
        if (n.Children.Count > 0) d["children"] = n.Children.Select(ToJson).ToList();
        return d;
    }
}
