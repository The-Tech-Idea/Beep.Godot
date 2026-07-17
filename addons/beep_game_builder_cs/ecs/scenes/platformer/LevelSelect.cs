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

            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level1Button").Pressed += () => GoToLevel(1);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level2Button").Pressed += () => GoToLevel(2);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level3Button").Pressed += () => GoToLevel(3);
        }

        /// <summary>Record the chosen level on GameApp, then load the gameplay scene — its
        /// LevelLoaderComponent reads CurrentLevel and instances that level. Before, every
        /// button loaded the same scene and the number was ignored.</summary>
        private void GoToLevel(int level)
        {
            GameApp.Instance?.SetLevel(level);
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
