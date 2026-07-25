using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PreLevel : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectButton("BackButton", () => ChangeScene(GameInfo.Instance?.LevelMapPath));
            this.ConnectButton("PlayButton", () => ChangeScene(GameApp.Instance?.GameScenePath));

            // Boosters are optional pre-level pickers: toggle them on, and the choice is
            // carried into the level via the GameStateManager data bag (booster_1..3). The
            // level scene reads GetGameData("booster_N") to apply the effect — that effect
            // is the game's, but selecting + carrying is real and complete here.
            WireBooster("Booster1", "booster_1");
            WireBooster("Booster2", "booster_2");
            WireBooster("Booster3", "booster_3");
        }

        private void WireBooster(string nodeName, string key)
        {
            if (this.Find<Button>(nodeName) is not { } button) return;

            button.ToggleMode = true;
            // Reflect any previously-saved selection.
            var manager = GameStateManagerComponent.Instance;
            button.ButtonPressed = manager?.GetGameData(key, false).AsBool() ?? false;

            button.Toggled += on => GameStateManagerComponent.Instance?.SetGameData(key, on);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
