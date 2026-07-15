using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class GameOver : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/GameOverVBox/RetryButton").Pressed    += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/GameOverVBox/MainMenuButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
