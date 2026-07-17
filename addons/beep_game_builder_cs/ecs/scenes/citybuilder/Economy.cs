using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Economy : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
        }

    }
}
