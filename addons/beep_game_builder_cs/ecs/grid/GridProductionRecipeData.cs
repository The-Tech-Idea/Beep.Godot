using Godot;
using static Beep.ECS.GridDefinitionReader;

namespace Beep.ECS;

/// <summary>Borrowed recipe values for execution; does not manufacture temporary Godot Resources.</summary>
internal sealed record GridProductionRecipeData(string RecipeId, float Duration,
    Godot.Collections.Array Inputs, Godot.Collections.Array Outputs)
{
    public float EffectiveDurationTurns => Mathf.Max(0.01f, float.IsFinite(Duration) ? Duration : 0.01f);
    public bool HasOutputs()
    {
        foreach (var (id, amount) in GridResourceAmount.Enumerate(Outputs))
            if (amount > 0 && !string.IsNullOrWhiteSpace(id)) return true;
        return false;
    }

    public static GridProductionRecipeData? Read(Variant entry)
    {
        if (entry.VariantType == Variant.Type.Dictionary)
        {
            var data = entry.AsGodotDictionary();
            return new(ReadString(data, "RecipeId", "recipe_id", "recipe"),
                ReadFloat(data, "DurationTurns", "duration_turns", 4),
                ReadArray(data, "Inputs", "inputs"), ReadArray(data, "Outputs", "outputs"));
        }
        if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource) return null;
        if (resource is GridProductionRecipe typed)
            return new(typed.RecipeId, typed.DurationTurns, typed.Inputs, typed.Outputs);
        return new(ReadString(resource, "RecipeId", "recipe_id", "recipe"),
            ReadFloat(resource, "DurationTurns", "duration_turns", 4),
            ReadArray(resource, "Inputs", "inputs"), ReadArray(resource, "Outputs", "outputs"));
    }
}
