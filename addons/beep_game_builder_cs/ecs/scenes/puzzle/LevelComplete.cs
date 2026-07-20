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

            // Bind real score + high-score state (BestScore is updated by GameApp when the session
            // ends). Star ratings are a genre-specific scoring rule the game supplies. (Scope.)
            if (GameApp.Instance is { } app)
            {
                if (GetNodeOrNull<Label>("Center/Panel/Margin/VBox/ScoreLabel") is { } score)
                    score.Text = $"Score: {app.SessionScore}";
                if (GetNodeOrNull<Label>("Center/Panel/Margin/VBox/HighScoreLabel") is { } high)
                    high.Visible = app.BestScore > 0 && app.SessionScore > app.BestScore;   // strictly greater — a tie isn't a new record
            }

            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/MapButton", () => ChangeScene(GameInfo.Instance?.LevelMapPath));
            // Retry replays the current level; Next advances first — otherwise both
            // buttons did exactly the same thing and "Next Level" replayed the level.
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/RetryButton", () => ChangeScene(GameApp.Instance?.GameScenePath));
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/NextButton", OnNextPressed);
        }

        /// <summary>Advance to the next level, then load the game scene.</summary>
        private void OnNextPressed()
        {
            var app = GameApp.Instance;
            if (app != null)
            {
                // Reaching this screen IS the completion — record it before advancing.
                app.CompleteLevel(app.CurrentLevel);
                app.SetLevel(app.CurrentLevel + 1);
            }
            ChangeScene(app?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
