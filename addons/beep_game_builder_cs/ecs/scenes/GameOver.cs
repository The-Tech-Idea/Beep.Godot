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

            GetNode<Button>("Center/GameOverVBox/RetryButton").Pressed    += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/GameOverVBox/MainMenuButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
