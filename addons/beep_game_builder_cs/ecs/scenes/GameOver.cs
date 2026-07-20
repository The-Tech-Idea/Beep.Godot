using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class GameOver : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Show the real session score (GameFlowComponent forwards score into GameApp during
            // play). Falls back to the scene literal only when there is no GameApp autoload.
            if (GameApp.Instance is { } app
                && GetNodeOrNull<Label>("Center/GameOverVBox/StatsLabel") is { } stats)
            {
                // Strictly greater — an exact tie isn't a new best, and this is a loss screen where
                // BestScore isn't updated anyway, so only a genuine record shows "(Best!)".
                stats.Text = app.BestScore > 0 && app.SessionScore > app.BestScore
                    ? $"Score: {app.SessionScore}  (Best!)"
                    : $"Score: {app.SessionScore}";
            }

            this.ConnectPressed("Center/GameOverVBox/RetryButton", () => ChangeScene(GameApp.Instance?.GameScenePath));
            this.ConnectPressed("Center/GameOverVBox/MainMenuButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
