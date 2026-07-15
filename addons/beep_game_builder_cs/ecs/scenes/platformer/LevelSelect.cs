using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelSelect : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level1Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level2Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/WorldTabs/World1/W1VBox/LevelGrid/Level3Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
