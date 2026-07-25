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

            this.ConnectButton("BackButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
            // One name per card. All four buttons used to be named "SelectButton", distinguished
            // only by their card's path — fine for a path lookup, ambiguous for a name lookup, so
            // the scene now names them per character. validate_scenes.sh fails on a duplicate.
            this.ConnectButton("MarineSelectButton", () => SelectCharacter("Marine"));
            this.ConnectButton("PilotSelectButton", () => SelectCharacter("Pilot"));
            this.ConnectButton("HunterSelectButton", () => SelectCharacter("Hunter"));
            this.ConnectButton("BruiserSelectButton", () => SelectCharacter("Bruiser"));
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
