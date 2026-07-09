using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

public static class McpJson
{
    public static Variant ToVariant(JsonNode? node)
    {
        if (node is null)
            return default;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out bool b)) return b;
            if (value.TryGetValue<int>(out int i)) return i;
            if (value.TryGetValue<long>(out long l)) return l;
            if (value.TryGetValue<float>(out float f)) return f;
            if (value.TryGetValue<double>(out double d)) return d;
            if (value.TryGetValue<string>(out string? s)) return s ?? string.Empty;
        }

        if (node is JsonArray array)
        {
            Godot.Collections.Array godotArray = new();
            foreach (JsonNode? item in array)
                godotArray.Add(ToVariant(item));
            return godotArray;
        }

        if (node is JsonObject obj)
        {
            if (LooksLikeVector2(obj)) return Vector2FromJson(obj, Vector2.Zero);
            if (LooksLikeVector3(obj)) return Vector3FromJson(obj, Vector3.Zero);
            if (LooksLikeColor(obj)) return ColorFromJson(obj, Colors.White);

            Godot.Collections.Dictionary dictionary = new();
            foreach (var kvp in obj)
                dictionary[kvp.Key] = ToVariant(kvp.Value);
            return dictionary;
        }

        return node.ToJsonString();
    }

    public static JsonNode? FromVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.NodePath => value.AsNodePath().ToString(),
            Variant.Type.Vector2 => Vector2ToJson(value.AsVector2()),
            Variant.Type.Vector2I => Vector2IToJson(value.AsVector2I()),
            Variant.Type.Vector3 => Vector3ToJson(value.AsVector3()),
            Variant.Type.Vector3I => Vector3IToJson(value.AsVector3I()),
            Variant.Type.Color => ColorToJson(value.AsColor()),
            Variant.Type.Rect2 => Rect2ToJson(value.AsRect2()),
            Variant.Type.Quaternion => QuaternionToJson(value.AsQuaternion()),
            Variant.Type.Basis => value.ToString(),
            Variant.Type.Transform2D => value.ToString(),
            Variant.Type.Transform3D => value.ToString(),
            Variant.Type.Array => ArrayToJson(value.AsGodotArray()),
            Variant.Type.Dictionary => DictionaryToJson(value.AsGodotDictionary()),
            Variant.Type.Object => ObjectToJson(value.AsGodotObject()),
            _ => value.ToString()
        };
    }

    public static JsonArray ArrayToJson(Godot.Collections.Array array)
    {
        JsonArray output = new();
        foreach (Variant item in array)
            output.Add(FromVariant(item));
        return output;
    }

    public static JsonObject DictionaryToJson(Godot.Collections.Dictionary dictionary)
    {
        JsonObject output = new();
        foreach (Variant key in dictionary.Keys)
        {
            string name = key.ToString();
            output[name] = FromVariant(dictionary[key]);
        }
        return output;
    }

    public static JsonNode? ObjectToJson(GodotObject? obj)
    {
        if (obj is null)
            return null;
        if (obj is Node node)
        {
            return new JsonObject
            {
                ["type"] = node.GetClass(),
                ["name"] = node.Name.ToString(),
                ["path"] = node.GetPath().ToString()
            };
        }
        if (obj is Resource resource)
        {
            return new JsonObject
            {
                ["type"] = resource.GetClass(),
                ["resource_path"] = resource.ResourcePath
            };
        }
        return obj.ToString();
    }

    public static JsonObject Vector2ToJson(Vector2 v) => new()
    {
        ["x"] = v.X,
        ["y"] = v.Y
    };

    public static JsonObject Vector2IToJson(Vector2I v) => new()
    {
        ["x"] = v.X,
        ["y"] = v.Y
    };

    public static JsonObject Vector3ToJson(Vector3 v) => new()
    {
        ["x"] = v.X,
        ["y"] = v.Y,
        ["z"] = v.Z
    };

    public static JsonObject Vector3IToJson(Vector3I v) => new()
    {
        ["x"] = v.X,
        ["y"] = v.Y,
        ["z"] = v.Z
    };

    public static JsonObject ColorToJson(Color c) => new()
    {
        ["r"] = c.R,
        ["g"] = c.G,
        ["b"] = c.B,
        ["a"] = c.A
    };

    public static JsonObject Rect2ToJson(Rect2 r) => new()
    {
        ["position"] = Vector2ToJson(r.Position),
        ["size"] = Vector2ToJson(r.Size)
    };

    public static JsonObject QuaternionToJson(Quaternion q) => new()
    {
        ["x"] = q.X,
        ["y"] = q.Y,
        ["z"] = q.Z,
        ["w"] = q.W
    };

    public static Vector2 Vector2FromJson(JsonObject? obj, Vector2 fallback)
    {
        if (obj is null)
            return fallback;
        return new Vector2(
            obj["x"]?.GetValue<float>() ?? fallback.X,
            obj["y"]?.GetValue<float>() ?? fallback.Y
        );
    }

    public static Vector3 Vector3FromJson(JsonObject? obj, Vector3 fallback)
    {
        if (obj is null)
            return fallback;
        return new Vector3(
            obj["x"]?.GetValue<float>() ?? fallback.X,
            obj["y"]?.GetValue<float>() ?? fallback.Y,
            obj["z"]?.GetValue<float>() ?? fallback.Z
        );
    }

    public static Color ColorFromJson(JsonObject? obj, Color fallback)
    {
        if (obj is null)
            return fallback;
        return new Color(
            obj["r"]?.GetValue<float>() ?? fallback.R,
            obj["g"]?.GetValue<float>() ?? fallback.G,
            obj["b"]?.GetValue<float>() ?? fallback.B,
            obj["a"]?.GetValue<float>() ?? fallback.A
        );
    }

    private static bool LooksLikeVector2(JsonObject obj) => obj.ContainsKey("x") && obj.ContainsKey("y") && !obj.ContainsKey("z") && !obj.ContainsKey("r");
    private static bool LooksLikeVector3(JsonObject obj) => obj.ContainsKey("x") && obj.ContainsKey("y") && obj.ContainsKey("z");
    private static bool LooksLikeColor(JsonObject obj) => obj.ContainsKey("r") && obj.ContainsKey("g") && obj.ContainsKey("b");
}
