using Godot;
using System.Globalization;

namespace Beep.ECS
{
    internal static class GridVariantReader
    {
        public static bool TryDictionary(Variant value, out Godot.Collections.Dictionary dictionary)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                dictionary = value.AsGodotDictionary();
                return true;
            }

            dictionary = new Godot.Collections.Dictionary();
            return false;
        }

        public static Godot.Collections.Array Array(Godot.Collections.Dictionary data, string key)
            => data.ContainsKey(key) && data[key].VariantType == Variant.Type.Array
                ? data[key].AsGodotArray()
                : new Godot.Collections.Array();

        public static string String(Godot.Collections.Dictionary data, string key, string fallback = "")
            => data.ContainsKey(key) ? data[key].AsString() : fallback;

        public static int Int(Godot.Collections.Dictionary data, string key, int fallback = 0)
            => data.ContainsKey(key) ? Int(data[key], fallback) : fallback;

        public static int Int(Variant value, int fallback = 0)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                    return value.AsInt32();
                case Variant.Type.Float:
                {
                    double raw = value.AsDouble();
                    return double.IsFinite(raw) ? Mathf.RoundToInt((float)raw) : fallback;
                }
                case Variant.Type.String:
                {
                    string text = value.AsString();
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                        return parsedInt;

                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFloat)
                        && float.IsFinite(parsedFloat)
                            ? Mathf.RoundToInt(parsedFloat)
                            : fallback;
                }
                default:
                    return fallback;
            }
        }

        public static float Float(Godot.Collections.Dictionary data, string key, float fallback = 0f)
            => data.ContainsKey(key) ? Float(data[key], fallback) : fallback;

        public static float Float(Variant value, float fallback = 0f)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                case Variant.Type.Float:
                {
                    double raw = value.AsDouble();
                    return double.IsFinite(raw) ? (float)raw : fallback;
                }
                case Variant.Type.String:
                    return float.TryParse(value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                        && float.IsFinite(parsed)
                            ? parsed
                            : fallback;
                default:
                    return fallback;
            }
        }

        public static bool Bool(Godot.Collections.Dictionary data, string key, bool fallback = false)
            => data.ContainsKey(key) ? Bool(data[key], fallback) : fallback;

        public static bool Bool(Variant value, bool fallback = false)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Bool:
                    return value.AsBool();
                case Variant.Type.Int:
                    return value.AsInt32() != 0;
                case Variant.Type.String:
                    return bool.TryParse(value.AsString(), out bool parsed) ? parsed : fallback;
                default:
                    return fallback;
            }
        }

        public static Vector2I Vector2I(Godot.Collections.Dictionary data, string key, Vector2I fallback)
            => data.ContainsKey(key) ? Vector2I(data[key], fallback) : fallback;

        public static Vector2I Vector2I(Variant value, Vector2I fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Vector2I:
                    return value.AsVector2I();
                case Variant.Type.Vector2:
                {
                    Vector2 raw = value.AsVector2();
                    return new Vector2I(Mathf.RoundToInt(raw.X), Mathf.RoundToInt(raw.Y));
                }
                case Variant.Type.Dictionary:
                {
                    Godot.Collections.Dictionary data = value.AsGodotDictionary();
                    if (data.ContainsKey("cell"))
                        return Vector2I(data["cell"], fallback);
                    int x = Int(data, "x", int.MinValue);
                    int y = Int(data, "y", int.MinValue);
                    if (x != int.MinValue && y != int.MinValue)
                        return new Vector2I(x, y);
                    x = Int(data, "X", int.MinValue);
                    y = Int(data, "Y", int.MinValue);
                    return x != int.MinValue && y != int.MinValue ? new Vector2I(x, y) : fallback;
                }
                default:
                    return fallback;
            }
        }
    }
}
