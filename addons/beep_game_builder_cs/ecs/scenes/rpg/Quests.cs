using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Quests : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Margin/VBox/Header/CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

    }
}
