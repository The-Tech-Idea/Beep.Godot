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
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/QuitButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath);
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

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
