using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class MainMenu : Control
    {
        public override void _Ready()
        {
            Connect("NewGameButton",  "res://scenes/main/main.tscn");
            Connect("ContinueButton", "res://scenes/main/main.tscn");
            Connect("LoadGameButton",  null);
            Connect("SettingsButton", "res://scenes/ui/settings_menu.tscn");
            Connect("CreditsButton",   null);
            Connect("StartButton",    "res://scenes/main/main.tscn");
            Connect("QuitButton",      null, quit: true);
        }

        private void Connect(string name, string? targetScene, bool quit = false)
        {
            var btn = FindChild(name, recursive: true, owned: false) as Button;
            if (btn == null) return;
            if (quit) btn.Pressed += () => GetTree().Quit();
            else if (targetScene != null) btn.Pressed += () => LoadScene(targetScene);
        }

        private void LoadScene(string path)
        {
            if (ResourceLoader.Exists(path))
                GetTree().ChangeSceneToFile(path);
        }
    }
}
