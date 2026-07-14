using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class GameOver : Control
    {
        public override void _Ready()
        {
            Connect("RetryButton",    null, restart: true);
            Connect("MainMenuButton", "res://scenes/ui/main_menu.tscn");
        }

        private void Connect(string name, string? target, bool restart = false)
        {
            var btn = FindChild(name, recursive: true, owned: false) as Button;
            if (btn == null) return;
            if (restart) btn.Pressed += () => GetTree().ReloadCurrentScene();
            else if (target != null) btn.Pressed += () => { if (ResourceLoader.Exists(target)) GetTree().ChangeSceneToFile(target); };
        }
    }
}
