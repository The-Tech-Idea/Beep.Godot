using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Crafting : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Each recipe button records the chosen recipe on GameStateManager and closes.
            // Consuming ingredients and granting the crafted item is the game's job (a
            // CraftingComponent path) — it reads GetGameData("craft_selection"). (Scope.)
            WireRecipe("Margin/VBox/RecipeGrid/Recipe1", "recipe_1");
            WireRecipe("Margin/VBox/RecipeGrid/Recipe2", "recipe_2");
            WireRecipe("Margin/VBox/RecipeGrid/Recipe3", "recipe_3");

            this.ConnectPressed("Margin/VBox/Header/BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireRecipe(string buttonPath, string recipeId)
        {
            if (GetNodeOrNull<Button>(buttonPath) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("craft_selection", recipeId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] Crafting: button '{buttonPath}' not found — that recipe is inert. Check the scene node name.");
        }
    }
}
