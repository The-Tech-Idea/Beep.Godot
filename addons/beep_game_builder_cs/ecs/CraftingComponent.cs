using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Recipe-based item crafting. Attach alongside InventoryComponent. Define
    /// recipes as CraftingRecipe resources (drag-and-drop in the inspector).
    /// Call Craft(recipe) to check materials, deduct them, and emit Crafted.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CraftingComponent : GameplayComponent
    {
        [Export] public CraftingRecipe[] Recipes { get; set; } = System.Array.Empty<CraftingRecipe>();

        [Signal] public delegate void CraftedEventHandler(string resultItemId);
        [Signal] public delegate void CraftFailedEventHandler(string reason);

        /// <summary>Check if a recipe can be crafted with the current inventory.</summary>
        public bool CanCraft(CraftingRecipe recipe, InventoryComponent inventory)
        {
            foreach (var input in recipe.InputItems)
                if (!inventory.HasItem(input.ItemId, input.Count))
                    return false;
            return true;
        }

        /// <summary>Craft a recipe: deduct materials, emit result. Returns true on success.</summary>
        public bool Craft(CraftingRecipe recipe, InventoryComponent inventory)
        {
            if (!IsActive) return false;
            if (!CanCraft(recipe, inventory))
            {
                EmitSignal(SignalName.CraftFailed, "Missing materials");
                return false;
            }
            // Deduct materials.
            foreach (var input in recipe.InputItems)
                inventory.RemoveItem(input.ItemId, input.Count);
            // Grant result.
            EmitSignal(SignalName.Crafted, recipe.OutputItem);
            return true;
        }
    }

    /// <summary>
    /// A crafting recipe resource. Drag-and-drop in the inspector on
    /// CraftingComponent.Recipes.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CraftingRecipe : Resource
    {
        [Export] public string RecipeName { get; set; } = "New Recipe";
        [Export] public CraftingIngredient[] InputItems { get; set; } = System.Array.Empty<CraftingIngredient>();
        [Export] public string OutputItem { get; set; } = "";
        [Export] public int OutputCount { get; set; } = 1;
        [Export] public float CraftTime { get; set; } = 0f;
    }

    /// <summary>A single ingredient in a crafting recipe.</summary>
    [Tool]
    [GlobalClass]
    public partial class CraftingIngredient : Resource
    {
        [Export] public string ItemId { get; set; } = "";
        [Export] public int Count { get; set; } = 1;
    }
}
