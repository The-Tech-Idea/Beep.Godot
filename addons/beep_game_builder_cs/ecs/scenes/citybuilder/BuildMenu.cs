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

            // Each build button records the chosen building on GameStateManager and closes. Placing
            // it in the world (cost, footprint, snapping) is the game's job — it reads
            // GetGameData("build_selection"). Same choice-record pattern as CharacterSelect. (Scope.)
            WireBuild("Margin/VBox/ItemGrid/Item1", "house");
            WireBuild("Margin/VBox/ItemGrid/Item2", "factory");
            WireBuild("Margin/VBox/ItemGrid/Item3", "park");

            this.ConnectPressed("Margin/VBox/CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireBuild(string buttonPath, string buildingId)
        {
            if (GetNodeOrNull<Button>(buttonPath) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("build_selection", buildingId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] BuildMenu: button '{buttonPath}' not found — that build option is inert. Check the scene node name.");
        }
    }
}
