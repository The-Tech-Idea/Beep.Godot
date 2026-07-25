using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Crafting : Control
    {
        /// <summary>GameData key holding the recipe the player committed to. Consuming
        /// ingredients and granting the crafted item is the game's job (a CraftingComponent
        /// path) — it reads GetGameData("craft_selection"). (Scope.)</summary>
        public const string SelectionKey = "craft_selection";

        private string _selectedRecipe = "";

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Picking a recipe fills the detail pane; Craft commits it and closes. This used to
            // record-and-close on the recipe press itself, which would leave the detail pane —
            // the whole right half of the screen — impossible to ever see.
            WireRecipe("Recipe1", "recipe_1");
            WireRecipe("Recipe2", "recipe_2");
            WireRecipe("Recipe3", "recipe_3");

            this.ConnectButton("CraftButton", OnCraft);
            this.ConnectButton("BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireRecipe(string buttonName, string recipeId)
        {
            if (this.Find<Button>(buttonName) is not { } btn)
            {
                GD.PushWarning($"[{Name}] Crafting: button '{buttonName}' not found — that recipe is inert. Check the scene node name.");
                return;
            }
            btn.Pressed += () =>
            {
                _selectedRecipe = recipeId;
                if (this.Find<Label>("RecipeTitle") is { } title) title.Text = btn.Text;
            };
        }

        private void OnCraft()
        {
            if (string.IsNullOrEmpty(_selectedRecipe))
            {
                GD.PushWarning($"[{Name}] Craft pressed with no recipe selected — nothing recorded.");
                return;
            }
            GameStateManagerComponent.Instance?.SetGameData(SelectionKey, _selectedRecipe);
            UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
        }
    }
}
