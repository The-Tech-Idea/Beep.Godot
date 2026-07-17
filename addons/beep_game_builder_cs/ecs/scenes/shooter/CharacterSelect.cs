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

            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
            GetNode<Button>("Margin/VBox/CharGrid/MarineCard/MarineVBox/SelectButton").Pressed += () => SelectCharacter("Marine");
            GetNode<Button>("Margin/VBox/CharGrid/PilotCard/PilotVBox/SelectButton").Pressed += () => SelectCharacter("Pilot");
            GetNode<Button>("Margin/VBox/CharGrid/HunterCard/HunterVBox/SelectButton").Pressed += () => SelectCharacter("Hunter");
            GetNode<Button>("Margin/VBox/CharGrid/BruiserCard/BruiserVBox/SelectButton").Pressed += () => SelectCharacter("Bruiser");
        }

        /// <summary>Record the picked character on GameApp, then start the run. Before, all four
        /// cards loaded the same scene and the choice was silently discarded.</summary>
        private void SelectCharacter(string character)
        {
            if (GameApp.Instance is { } app) app.SelectedCharacter = character;
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.</summary>
        private void ChangeScene(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Navigation target does not exist: {path}");
                return;
            }
            Error err = GetTree().ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{Name}] Failed to change scene to {path}: {err}");
        }
    }
}
