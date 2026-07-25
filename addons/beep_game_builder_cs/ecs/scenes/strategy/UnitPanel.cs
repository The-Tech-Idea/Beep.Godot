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
            WireAction("Action1", "move");
            WireAction("Action2", "attack");
            WireAction("Action3", "defend");
            WireAction("Action4", "special");

            this.ConnectButton("CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireAction(string buttonName, string actionId)
        {
            if (this.Find<Button>(buttonName) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("unit_action", actionId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] UnitPanel: button '{buttonName}' not found — that action is inert. Check the scene node name.");
        }
    }
}
