using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

public static class McpTreeSerializer
{
    public static JsonObject Serialize(Node root, int maxDepth = 8)
    {
        return SerializeNode(root, 0, maxDepth);
    }

    public static JsonObject SerializeSingle(Node node)
    {
        JsonObject output = new()
        {
            ["name"] = node.Name.ToString(),
            ["type"] = node.GetClass(),
            ["path"] = node.GetPath().ToString(),
            ["child_count"] = node.GetChildCount(),
            ["owner"] = node.Owner?.GetPath().ToString(),
            ["scene_file_path"] = node.SceneFilePath,
            ["process_mode"] = node.ProcessMode.ToString(),
            ["groups"] = Groups(node)
        };

        if (node is Node2D node2D)
        {
            output["position"] = McpJson.Vector2ToJson(node2D.Position);
            output["global_position"] = McpJson.Vector2ToJson(node2D.GlobalPosition);
            output["rotation"] = node2D.Rotation;
            output["scale"] = McpJson.Vector2ToJson(node2D.Scale);
        }

        if (node is Control control)
        {
            output["position"] = McpJson.Vector2ToJson(control.Position);
            output["size"] = McpJson.Vector2ToJson(control.Size);
            output["visible"] = control.Visible;
        }

        return output;
    }

    private static JsonObject SerializeNode(Node node, int depth, int maxDepth)
    {
        JsonObject output = SerializeSingle(node);
        JsonArray children = new();

        if (depth < maxDepth)
        {
            foreach (Node child in node.GetChildren())
                children.Add(SerializeNode(child, depth + 1, maxDepth));
        }
        else if (node.GetChildCount() > 0)
        {
            output["truncated"] = true;
        }

        output["children"] = children;
        return output;
    }

    private static JsonArray Groups(Node node)
    {
        JsonArray groups = new();
        foreach (StringName group in node.GetGroups())
            groups.Add(group.ToString());
        return groups;
    }
}
