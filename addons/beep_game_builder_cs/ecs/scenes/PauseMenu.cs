using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PauseMenu : Control
    {
        public override void _Ready()
        {
            Connect("ResumeButton",    null, resume: true);
            Connect("SaveGameButton",  null);
            Connect("LoadGameButton",  null);
            Connect("SettingsButton",  "res://scenes/ui/settings_menu.tscn");
            Connect("RestartButton",   null, restart: true);
            Connect("MainMenuButton",  "res://scenes/ui/main_menu.tscn");
            Connect("QuitButton",      null, quit: true);
        }

        private void Connect(string name, string? target, bool quit = false, bool resume = false, bool restart = false)
        {
            var btn = FindChild(name, recursive: true, owned: false) as Button;
            if (btn == null) return;
            if (quit)       btn.Pressed += () => GetTree().Quit();
            else if (resume)  btn.Pressed += () => Hide();
            else if (restart) btn.Pressed += () => GetTree().ReloadCurrentScene();
            else if (target != null) btn.Pressed += () => { if (ResourceLoader.Exists(target)) GetTree().ChangeSceneToFile(target); };
        }
    }
}
