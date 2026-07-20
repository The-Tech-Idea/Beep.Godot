using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelSelect : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Margin/VBox/Header/BackButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
            this.ConnectPressed("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level1Button", () => GoToLevel(1));
            this.ConnectPressed("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level2Button", () => GoToLevel(2));
            // Level 3 is wired but its button ships disabled: platformer_main's LevelLoader
            // only has level_1 and level_2, so picking 3 hit "No level scene for level 3" and
            // dropped the player onto an empty stage. Add levels/platformer/level_3.tscn to
            // the loader's Levels array, then re-enable the button in level_select.tscn.
            this.ConnectPressed("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level3Button", () => GoToLevel(3));
        }

        /// <summary>Record the chosen level on GameApp, then load the gameplay scene — its
        /// LevelLoaderComponent reads CurrentLevel and instances that level. Before, every
        /// button loaded the same scene and the number was ignored.</summary>
        private void GoToLevel(int level)
        {
            GameApp.Instance?.SetLevel(level);
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
