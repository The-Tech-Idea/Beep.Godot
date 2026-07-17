using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelResults : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/NextButton").Pressed += OnNextLevel;
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelSelectPath ?? "res://scenes/ui/platformer/level_select.tscn");
        }

        /// <summary>Advance to the next level, then reload the gameplay scene (its LevelLoader
        /// reads CurrentLevel). Before, "Next" loaded the same GameScenePath as "Retry" without
        /// advancing, so it just replayed the current level.</summary>
        private void OnNextLevel()
        {
            if (GameApp.Instance is { } app) app.SetLevel(app.CurrentLevel + 1);
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
