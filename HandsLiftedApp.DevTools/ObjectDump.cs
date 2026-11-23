using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;

namespace HandsLiftedApp.DevTools;

/// <summary>Read-only reflection walker: dump a view-model/object graph by path, to a bounded depth. Never calls methods.</summary>
public static class ObjectDump
{
    private const int MaxItems = 25;

    public static object? Resolve(object root, string? path)
    {
        var cur = root;
        if (string.IsNullOrWhiteSpace(path)) return cur;

        foreach (var rawPart in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur == null) throw new DevToolsException($"'{path}': null before '{rawPart}'");

            var part = rawPart;
            int? index = null;
            var bracket = part.IndexOf('[');
            if (bracket >= 0 && part.EndsWith(']'))
            {
                if (!int.TryParse(part[(bracket + 1)..^1], out var i)) throw new DevToolsException($"bad index in '{rawPart}'");
                index = i;
                part = part[..bracket];
            }

            if (part.Length > 0)
            {
                var type = cur.GetType();
                var prop = type.GetProperty(part, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop != null && prop.GetIndexParameters().Length == 0) cur = prop.GetValue(cur);
                else if (type.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase) is { } f) cur = f.GetValue(cur);
                else throw new DevToolsException($"'{path}': {type.Name} has no public property '{part}'", 404,
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.GetIndexParameters().Length == 0).Select(p => p.Name).Take(60));
            }

            if (index != null)
            {
                if (cur is IList list)
                {
                    if (index < 0 || index >= list.Count) throw new DevToolsException($"'{path}': index {index} out of range (count {list.Count})");
                    cur = list[index.Value];
                }
                else if (cur is IEnumerable en) cur = en.Cast<object?>().ElementAtOrDefault(index.Value);
                else throw new DevToolsException($"'{path}': '{part}' is not a collection");
            }
        }
        return cur;
    }

    public static object? Dump(object? value, int depth) => Dump(value, depth, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static object? Dump(object? value, int depth, HashSet<object> seen)
    {
        switch (value)
        {
            case null: return null;
            case string s: return s.Length > 300 ? s[..300] + "…" : s;
            case bool or byte or short or int or long or float or double or decimal or Guid or DateTime or DateTimeOffset or TimeSpan: return value;
            case Enum e: return e.ToString();
            case Type t: return "typeof(" + t.Name + ")";
            case Delegate or ICommand or Task or IObservable<object> or AvaloniaObject { } when value is not INotifyPropertyChanged && depth <= 0:
                return "<" + value.GetType().Name + ">";
        }

        if (value is Delegate or ICommand or Task) return "<" + value.GetType().Name + ">";
        if (value.GetType().Name.Contains("Bitmap", StringComparison.Ordinal)) return "<" + value.GetType().Name + ">";
        if (!seen.Add(value)) return "<cycle " + value.GetType().Name + ">";

        try
        {
            if (value is IEnumerable en)
            {
                var items = en.Cast<object?>().ToList();
                if (depth <= 0) return new { type = value.GetType().Name, count = items.Count };
                return new
                {
                    type = value.GetType().Name,
                    count = items.Count,
                    items = items.Take(MaxItems).Select(i => Dump(i, depth - 1, seen)).ToList(),
                    more = items.Count > MaxItems ? items.Count - MaxItems : (int?)null,
                };
            }

            if (depth <= 0) return new { type = value.GetType().Name, text = SafeToString(value) };

            var props = new Dictionary<string, object?> { ["$type"] = value.GetType().Name };
            foreach (var p in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
                if (p.DeclaringType == typeof(AvaloniaObject) || p.Name is "Dispatcher" or "Changed" or "ThrownExceptions") continue;
                try { props[p.Name] = Dump(p.GetValue(value), depth - 1, seen); }
                catch (Exception ex) { props[p.Name] = "<error: " + (ex.InnerException ?? ex).Message + ">"; }
            }
            return props;
        }
        finally
        {
            seen.Remove(value);
        }
    }

    private static string SafeToString(object value)
    {
        try { var s = value.ToString() ?? ""; return s.Length > 200 ? s[..200] + "…" : s; }
        catch { return "<tostring failed>"; }
    }
}
