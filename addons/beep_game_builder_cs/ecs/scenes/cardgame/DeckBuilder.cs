using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class DeckBuilder : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Margin/VBox/Header/StartBattleButton").Pressed += () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
        }

    }
}
