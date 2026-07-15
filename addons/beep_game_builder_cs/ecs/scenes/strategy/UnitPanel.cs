using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class UnitPanel : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("Margin/VBox/CloseButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
