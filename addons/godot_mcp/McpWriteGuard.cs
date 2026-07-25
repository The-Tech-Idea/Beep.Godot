using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace GodotMcp;

/// <summary>
/// The checks every write must pass before it touches a node.
///
/// Godot is extremely permissive here: <c>node.Set("nonsense", v)</c> discards the value
/// and reports nothing, so a bridge that just forwards the call answers "updated: true"
/// for a write that did nothing at all. That is the single worst thing an agent-facing
/// API can do, and it is the repo's documented dominant defect class.
///
/// These guards were proven in BeepMcpSceneCommands first; they belong here so every
/// write path shares them.
/// </summary>
public static class McpWriteGuard
{
    /// <summary>Throw unless <paramref name="property"/> is a real, settable property on
    /// the node — naming the PascalCase form when the caller used snake_case for a C#
    /// [Export], which Godot would silently drop.</summary>
    public static void ValidateProperty(Node node, string property)
    {
        var names = PropertyNames(node);
        if (names.Contains(property)) return;

        if (property.Contains('_'))
        {
            string pascal = ToPascalCase(property);
            if (names.Contains(pascal))
                throw McpBridgeException.SnakeCaseExport(property, pascal);
        }

        throw McpBridgeException.UnknownProperty(node.GetType().Name, property);
    }

    // NOTE: there is deliberately no post-set "did the type survive?" check. Godot coerces
    // legitimately and constantly (int -> float, int -> enum, String -> NodePath), so
    // comparing VariantType before and after would flag correct writes far more often than
    // wrong ones. TYPE_MISMATCH stays in the code table for a checker that can tell the
    // difference -- using the property's DECLARED type from GetPropertyList rather than the
    // coerced result. Guessing here would be worse than not checking.

    /// <summary>Refuse to delete a node another node's NodePath export still points at.
    /// Godot resolves such a path to null afterwards without a word, which is exactly how
    /// this framework's components fail quietly.</summary>
    public static void EnsureNotReferenced(Node root, Node target)
    {
        var referrers = new List<string>();
        Collect(root, root, target, referrers);
        if (referrers.Count == 0) return;
        throw McpBridgeException.StillReferenced(root.GetPathTo(target).ToString(), string.Join(", ", referrers));

        static void Collect(Node root, Node current, Node target, List<string> into)
        {
            foreach (var prop in current.GetPropertyList())
            {
                if (!prop.ContainsKey("type") || !prop.ContainsKey("name")) continue;
                if ((Variant.Type)(int)prop["type"] != Variant.Type.NodePath) continue;

                string name = prop["name"].AsString();
                NodePath np = current.Get(name).AsNodePath();
                if (np.IsEmpty) continue;
                if (current.GetNodeOrNull(np) == target)
                    into.Add($"{root.GetPathTo(current)}.{name}");
            }
            foreach (var child in current.GetChildren()) Collect(root, child, target, into);
        }
    }

    public static HashSet<string> PropertyNames(GodotObject obj)
    {
        var set = new HashSet<string>();
        foreach (var p in obj.GetPropertyList())
            if (p.ContainsKey("name")) set.Add(p["name"].AsString());
        return set;
    }

    /// <summary>"title_label_path" → "TitleLabelPath".</summary>
    public static string ToPascalCase(string snake)
    {
        var sb = new StringBuilder();
        foreach (var part in snake.Split('_', System.StringSplitOptions.RemoveEmptyEntries))
            sb.Append(char.ToUpperInvariant(part[0])).Append(part.Length > 1 ? part[1..] : "");
        return sb.ToString();
    }
}
