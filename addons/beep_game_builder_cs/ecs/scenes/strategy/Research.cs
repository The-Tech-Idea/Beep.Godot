using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Research : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Each tech button records the chosen research on GameStateManager and closes. Applying
            // the tech's effect (unlocks, bonuses, tree gating) is the game's job — it reads
            // GetGameData("research_selection"). (Scope.)
            WireTech("Tech1", "tech_1");
            WireTech("Tech2", "tech_2");
            WireTech("Tech3", "tech_3");
            WireTech("Tech4", "tech_4");

            this.ConnectButton("BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireTech(string buttonName, string techId)
        {
            if (this.Find<Button>(buttonName) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("research_selection", techId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] Research: button '{buttonName}' not found — that tech is inert. Check the scene node name.");
        }
    }
}
