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
            WireTech("Margin/VBox/TechGrid/Tech1", "tech_1");
            WireTech("Margin/VBox/TechGrid/Tech2", "tech_2");
            WireTech("Margin/VBox/TechGrid/Tech3", "tech_3");
            WireTech("Margin/VBox/TechGrid/Tech4", "tech_4");

            this.ConnectPressed("Margin/VBox/Header/BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireTech(string buttonPath, string techId)
        {
            if (GetNodeOrNull<Button>(buttonPath) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("research_selection", techId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] Research: button '{buttonPath}' not found — that tech is inert. Check the scene node name.");
        }
    }
}
