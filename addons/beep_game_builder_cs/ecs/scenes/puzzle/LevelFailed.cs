using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    /// <summary>Puzzle "out of moves" screen. The score/progress labels (e.g. "980 / 1500") show
    /// scene placeholders — progress toward a level's target is a genre-specific scoring rule the
    /// game supplies; the framework tracks only GameApp.SessionScore. The retry-bonus flow below IS
    /// wired. See CLAUDE.md § Scope.</summary>
    [Tool]
    [GlobalClass]
    public partial class LevelFailed : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Center/Panel/Margin/VBox/RetryBonusButton", OnRetryWithBonus);
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/QuitButton", () => ChangeScene(GameInfo.Instance?.LevelMapPath));
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/RetryButton", OnRetry);
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
