using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class UnitPanel : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Each action button records the chosen unit action on GameStateManager and closes.
            // Executing it (pathing, targeting, resolving the order) is the game's job — it reads
            // GetGameData("unit_action"). (Scope.)
            WireAction("Margin/VBox/ActionGrid/Action1", "move");
            WireAction("Margin/VBox/ActionGrid/Action2", "attack");
            WireAction("Margin/VBox/ActionGrid/Action3", "defend");
            WireAction("Margin/VBox/ActionGrid/Action4", "special");

            this.ConnectPressed("Margin/VBox/CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireAction(string buttonPath, string actionId)
        {
            if (GetNodeOrNull<Button>(buttonPath) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("unit_action", actionId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] UnitPanel: button '{buttonPath}' not found — that action is inert. Check the scene node name.");
        }
    }
}
