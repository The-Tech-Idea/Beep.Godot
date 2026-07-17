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

            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath);
            // Retry replays the current level; Next advances first — otherwise both
            // buttons did exactly the same thing and "Next Level" replayed the level.
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/NextButton").Pressed += OnNextPressed;
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
