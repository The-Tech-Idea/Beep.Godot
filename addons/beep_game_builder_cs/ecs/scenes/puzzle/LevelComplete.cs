using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelComplete : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath ?? "res://scenes/ui/puzzle/level_map.tscn");
            // Retry replays the current level; Next advances first — otherwise both
            // buttons did exactly the same thing and "Next Level" replayed the level.
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/NextButton").Pressed += OnNextPressed;
        }

        /// <summary>Advance to the next level, then load the game scene.</summary>
        private void OnNextPressed()
        {
            var app = GameApp.Instance;
            app?.SetLevel(app.CurrentLevel + 1);
            ChangeScene(app?.GameScenePath);
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
