using Godot;
using static Beep.ECS.GridDefinitionReader;

namespace Beep.ECS
{
    /// <summary>
    /// Data-driven production recipe for top-down/isometric settlement buildings.
    /// Use with GridProductionComponent to convert wallet resources over time.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProductionRecipe : Resource
    {
        [Export] public string RecipeId { get; set; } = "planks";
        [Export] public string DisplayName { get; set; } = "Planks";
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float DurationSeconds { get; set; } = 4f;
        [Export] public Godot.Collections.Array Inputs { get; set; } = new();
        [Export] public Godot.Collections.Array Outputs { get; set; } = new();

        public float EffectiveDurationSeconds => Mathf.Max(0.01f, float.IsFinite(DurationSeconds) ? DurationSeconds : 0.01f);

        public bool HasOutputs()
        {
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(Outputs))
                if (amount > 0 && !string.IsNullOrWhiteSpace(resourceId))
                    return true;
            return false;
        }

        public static System.Collections.Generic.IEnumerable<GridProductionRecipe> Enumerate(Godot.Collections.Array recipes)
        {
            foreach (Variant entry in recipes)
                if (TryRead(entry, out GridProductionRecipe? recipe) && recipe != null)
                    yield return recipe;
        }

        public static bool TryRead(Variant entry, out GridProductionRecipe? recipe)
        {
            recipe = null;
            if (entry.VariantType == Variant.Type.Dictionary)
            {
                if (!GridVariantReader.TryDictionary(entry, out Godot.Collections.Dictionary data))
                    return false;

                recipe = new GridProductionRecipe
                {
                    RecipeId = ReadString(data, "RecipeId", "recipe_id", "recipe"),
                    DisplayName = ReadString(data, "DisplayName", "display_name", ""),
                    DurationSeconds = ReadFloat(data, "DurationSeconds", "duration_seconds", 4f),
                    Inputs = ReadArray(data, "Inputs", "inputs"),
                    Outputs = ReadArray(data, "Outputs", "outputs")
                };
                return !string.IsNullOrWhiteSpace(recipe.RecipeId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;
            if (resource is GridProductionRecipe typed)
            {
                recipe = typed;
                return !string.IsNullOrWhiteSpace(typed.RecipeId);
            }

            recipe = new GridProductionRecipe
            {
                RecipeId = ReadString(resource, "RecipeId", "recipe_id", "recipe"),
                DisplayName = ReadString(resource, "DisplayName", "display_name", ""),
                DurationSeconds = ReadFloat(resource, "DurationSeconds", "duration_seconds", 4f),
                Inputs = ReadArray(resource, "Inputs", "inputs"),
                Outputs = ReadArray(resource, "Outputs", "outputs")
            };
            return !string.IsNullOrWhiteSpace(recipe.RecipeId);
        }

        // Reading is delegated to GridDefinitionReader - the shared dual-key
        // (PascalCase / snake_case) reader all definition resources use.
    }
}
