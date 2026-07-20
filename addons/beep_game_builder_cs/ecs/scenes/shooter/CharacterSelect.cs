using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class CharacterSelect : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Margin/VBox/Header/BackButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
            this.ConnectPressed("Margin/VBox/CharGrid/MarineCard/MarineVBox/SelectButton", () => SelectCharacter("Marine"));
            this.ConnectPressed("Margin/VBox/CharGrid/PilotCard/PilotVBox/SelectButton", () => SelectCharacter("Pilot"));
            this.ConnectPressed("Margin/VBox/CharGrid/HunterCard/HunterVBox/SelectButton", () => SelectCharacter("Hunter"));
            this.ConnectPressed("Margin/VBox/CharGrid/BruiserCard/BruiserVBox/SelectButton", () => SelectCharacter("Bruiser"));
        }

        /// <summary>Record the picked character on GameApp, then start the run. Before, all four
        /// cards loaded the same scene and the choice was silently discarded.</summary>
        private void SelectCharacter(string character)
        {
            if (GameApp.Instance is { } app) app.SelectedCharacter = character;
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
