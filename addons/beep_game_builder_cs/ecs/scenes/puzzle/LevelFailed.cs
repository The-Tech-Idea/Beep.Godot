using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelFailed : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/Panel/Margin/VBox/RetryBonusButton").Pressed += OnRetryWithBonus;
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/QuitButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath ?? "res://scenes/ui/puzzle/level_map.tscn");
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += OnRetry;
        }

        private void OnRetry()
        {
            // Plain retry: make sure no bonus is carried over from a previous attempt.
            GameStateManagerComponent.Instance?.SetGameData("retry_bonus", false);
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        /// <summary>Retry the level with a bonus. Sets a flag the level reads
        /// (GetGameData("retry_bonus")) to grant its advantage — extra moves/lives/etc., the
        /// game's to define. The distinction from plain Retry (the flag) is real and carried.</summary>
        private void OnRetryWithBonus()
        {
            GameStateManagerComponent.Instance?.SetGameData("retry_bonus", true);
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
