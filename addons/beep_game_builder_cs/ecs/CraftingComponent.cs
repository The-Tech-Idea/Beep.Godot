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
                if (input.Item == null || !inventory.HasItem(input.Item.Id, input.Count))
                    return false;
            return true;
        }

        /// <summary>Craft a recipe: deduct materials, grant the output, emit result. Returns true
        /// on success.</summary>
        public bool Craft(CraftingRecipe recipe, InventoryComponent inventory)
        {
            if (!IsActive) return false;
            if (recipe.OutputItem == null)
            {
                GD.PushWarning($"[{Name}] Recipe '{recipe.RecipeName}' has no OutputItem — nothing to grant. Set CraftingRecipe.OutputItem.");
                EmitSignal(SignalName.CraftFailed, "Recipe has no output");
                return false;
            }
            if (!CanCraft(recipe, inventory))
            {
                EmitSignal(SignalName.CraftFailed, "Missing materials");
                return false;
            }
            // Refuse BEFORE consuming inputs if the product won't fit — otherwise the materials are
            // shredded into a full inventory and nothing is produced.
            if (!inventory.CanFit(recipe.OutputItem, recipe.OutputCount))
            {
                EmitSignal(SignalName.CraftFailed, "No room for the crafted item");
                return false;
            }
            // Deduct materials (CanCraft guaranteed every input.Item is non-null).
            foreach (var input in recipe.InputItems)
                inventory.RemoveItem(input.Item!.Id, input.Count);
            // Grant result. Without this the recipe consumed the inputs and produced nothing —
            // crafting was a material shredder.
            inventory.AddItem(recipe.OutputItem, recipe.OutputCount);
            EmitSignal(SignalName.Crafted, recipe.OutputItem.Id);
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
        [Export] public GameItem? OutputItem { get; set; }
        [Export] public int OutputCount { get; set; } = 1;
        [Export] public float CraftTime { get; set; } = 0f;
    }

    /// <summary>A single ingredient in a crafting recipe — a GameItem `.tres` and a count.</summary>
    [Tool]
    [GlobalClass]
    public partial class CraftingIngredient : Resource
    {
        [Export] public GameItem? Item { get; set; }
        [Export] public int Count { get; set; } = 1;
    }
}
