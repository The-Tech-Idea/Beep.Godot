using Godot;

namespace Beep.ECS.Scenes
{
    /// <summary>Race results screen. The Time / Placement / Reward labels show scene placeholders:
    /// lap time, finishing position and payout are racing-specific stats the framework does not
    /// track (unlike GameApp.SessionScore). The game fills them from its own race state — this
    /// screen ships the layout and the navigation, not the numbers. See CLAUDE.md § Scope.</summary>
    [Tool]
    [GlobalClass]
    public partial class RaceResults : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/RetryButton", () => ChangeScene(GameApp.Instance?.GameScenePath));
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/MenuButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
