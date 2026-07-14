using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class SettingsMenu : CanvasLayer
    {
        public override void _Ready()
        {
            Connect("CloseButton", null, back: true);
            Connect("BackButton",  null, back: true);
        }

        private void Connect(string name, string? target, bool back = false)
        {
            var btn = FindChild(name, recursive: true, owned: false) as Button;
            if (btn == null) return;
            if (back) btn.Pressed += () => { if (GetParent() is Control p) p.Visible = false; else QueueFree(); };
        }
    }
}
