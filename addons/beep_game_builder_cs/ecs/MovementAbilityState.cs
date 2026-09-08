using Godot;
using System;
using Dictionary = Godot.Collections.Dictionary;

namespace Beep.ECS;

/// <summary>Versioned, JSON-portable runtime state shared by movement ability save participants.</summary>
internal static class MovementAbilityState
{
    public static void Write(GameBuilder.GameStateData state, string key, bool active, Dictionary data)
    {
        data["version"] = 1;
        data["active"] = active;
        state.GameData[key] = data;
    }

    public static Dictionary Read(GameBuilder.GameStateData state, string key)
    {
        if (!state.GameData.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Dictionary)
            throw new FormatException($"Missing movement ability state: {key}.");
        var data = value.AsGodotDictionary();
        if (Count(data, "version") != 1) throw new FormatException($"Unsupported movement ability state: {key}.");
        return data;
    }

    public static float Number(Dictionary data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value.VariantType is not (Variant.Type.Int or Variant.Type.Float))
            throw new FormatException($"Invalid movement ability number: {key}.");
        double number = value.AsDouble();
        if (!double.IsFinite(number) || Math.Abs(number) > float.MaxValue)
            throw new FormatException($"Non-finite movement ability number: {key}.");
        return (float)number;
    }

    public static float Time(Dictionary data, string key)
    {
        float value = Number(data, key);
        if (value < 0) throw new FormatException($"Negative movement ability timer: {key}.");
        return value;
    }

    public static int Count(Dictionary data, string key)
    {
        Number(data, key);
        double value = data[key].AsDouble();
        if (value < 0 || value > int.MaxValue || value != Math.Truncate(value))
            throw new FormatException($"Invalid movement ability count: {key}.");
        return (int)value;
    }

    public static bool Flag(Dictionary data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Bool)
            throw new FormatException($"Invalid movement ability flag: {key}.");
        return value.AsBool();
    }

    public static Vector2 Vector(Dictionary data) => new(Number(data, "x"), Number(data, "y"));

    public static int Direction(Dictionary data, string key)
    {
        Number(data, key);
        double value = data[key].AsDouble();
        if (value != -1 && value != 0 && value != 1)
            throw new FormatException($"Invalid movement ability direction: {key}.");
        return (int)value;
    }
}
