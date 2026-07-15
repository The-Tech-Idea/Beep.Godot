using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class MainMenu : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/MenuVBox/NewGameButton").Pressed  += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/MenuVBox/ContinueButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/MenuVBox/LoadGameButton").Pressed  += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/MenuVBox/SettingsButton").Pressed += () => ChangeScene(GameApp.Instance?.SettingsScenePath);
            GetNode<Button>("Center/MenuVBox/QuitButton").Pressed     += () => GetTree().Quit();
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
