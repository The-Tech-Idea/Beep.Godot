using Godot;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Generic "level complete" results screen — a reusable, genre-agnostic destination for
    /// GameFlow.LevelComplete when a genre has no dedicated results screen of its own. Continue
    /// advances to the next level and reloads the gameplay scene; Menu returns to the main menu.
    ///
    /// Wired as LevelCompletePath for the action genres that previously fell through to
    /// game_over on completion (which read as a loss). Genres with a richer results screen
    /// (platformer level_results, shooter run_results, puzzle level_complete) keep theirs.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LevelSummary : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/ContinueButton").Pressed += OnContinue;
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MenuButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        private void OnContinue()
        {
            if (GameApp.Instance is { } app)
            {
                // Reaching this screen IS the completion — record it, then advance so the
                // reloaded gameplay scene's LevelLoader (which reads CurrentLevel) opens the next.
                app.CompleteLevel(app.CurrentLevel);
                app.SetLevel(app.CurrentLevel + 1);
            }
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
