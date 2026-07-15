using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class CharacterSelect : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
            GetNode<Button>("Margin/VBox/CharGrid/MarineCard/MarineVBox/SelectButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/CharGrid/PilotCard/PilotVBox/SelectButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/CharGrid/HunterCard/HunterVBox/SelectButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/CharGrid/BruiserCard/BruiserVBox/SelectButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
