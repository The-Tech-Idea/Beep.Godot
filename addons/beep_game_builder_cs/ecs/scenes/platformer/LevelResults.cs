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
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelSelectPath);
        }

        /// <summary>Advance to the next level, then reload the gameplay scene (its LevelLoader
        /// reads CurrentLevel). Before, "Next" loaded the same GameScenePath as "Retry" without
        /// advancing, so it just replayed the current level.</summary>
        private void OnNextLevel()
        {
            if (GameApp.Instance is { } app)
            {
                // Reaching this screen IS the completion — record it before advancing.
                // SetLevel used to mark its argument completed, so this marked the level the
                // player was about to start and never the one they just beat.
                app.CompleteLevel(app.CurrentLevel);
                app.SetLevel(app.CurrentLevel + 1);
            }
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
