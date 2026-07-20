using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class RunResults : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Bind the framework-tracked score. Time/Floor/Kills/Gold are genre-specific stats the
            // framework does NOT track — the game fills those (e.g. from its own run state); the
            // scene ships placeholder literals as a starting point. See CLAUDE.md § Scope.
            if (GameApp.Instance is { } app
                && GetNodeOrNull<Label>("Center/Panel/Margin/VBox/StatsGrid/ScoreValue") is { } scoreValue)
                scoreValue.Text = app.SessionScore.ToString();

            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/RetryButton", () => ChangeScene(GameApp.Instance?.GameScenePath));
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/MenuButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
