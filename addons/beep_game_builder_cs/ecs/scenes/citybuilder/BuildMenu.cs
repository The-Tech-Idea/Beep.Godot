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
            WireBuild("Item1", "house");
            WireBuild("Item2", "factory");
            WireBuild("Item3", "park");

            this.ConnectButton("CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireBuild(string buttonName, string buildingId)
        {
            if (this.Find<Button>(buttonName) is { } btn)
                btn.Pressed += () =>
                {
                    GameStateManagerComponent.Instance?.SetGameData("build_selection", buildingId);
                    UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                };
            else
                GD.PushWarning($"[{Name}] BuildMenu: button '{buttonName}' not found — that build option is inert. Check the scene node name.");
        }
    }
}
