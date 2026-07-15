using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PauseMenu : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Panel/PauseVBox/ResumeButton").Pressed   += () => { GetTree().Paused = false; QueueFree(); };
            GetNode<Button>("Panel/PauseVBox/SettingsButton").Pressed += () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.SettingsScenePath); };
            GetNode<Button>("Panel/PauseVBox/RestartButton").Pressed  += () => { GetTree().Paused = false; GetTree().ReloadCurrentScene(); };
            GetNode<Button>("Panel/PauseVBox/MainMenuButton").Pressed += () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.MainMenuPath); };
            GetNode<Button>("Panel/PauseVBox/QuitButton").Pressed     += () => GetTree().Quit();

            if (GetNodeOrNull<Button>("Panel/PauseVBox/SaveGameButton") is Button saveBtn)
                saveBtn.Pressed += () => GD.Print("TODO: Save game not yet implemented");
            if (GetNodeOrNull<Button>("Panel/PauseVBox/LoadGameButton") is Button loadBtn)
                loadBtn.Pressed += () => GD.Print("TODO: Load game not yet implemented");
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
