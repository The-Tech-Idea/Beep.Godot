using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class BuildMenu : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Margin/VBox/CloseButton").Pressed += () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
        }

    }
}
