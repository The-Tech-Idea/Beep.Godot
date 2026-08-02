using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Research : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            if (this.Find<KitTree>("ResearchTree") is { } tree)
                tree.NodeActivated += index => SelectTech($"tech_{index + 1}");
            else
            {
                // Backward compatibility for projects that kept an older copied research scene.
                WireTech("Tech1", "tech_1");
                WireTech("Tech2", "tech_2");
                WireTech("Tech3", "tech_3");
                WireTech("Tech4", "tech_4");
            }

            this.ConnectButton("BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));
        }

        private void WireTech(string buttonName, string techId)
        {
            if (this.Find<Button>(buttonName) is { } btn)
                btn.Pressed += () => SelectTech(techId);
            else
                GD.PushWarning($"[{Name}] Research: button '{buttonName}' not found — that tech is inert. Check the scene node name.");
        }

        private void SelectTech(string techId)
        {
            GameStateManagerComponent.Instance?.SetGameData("research_selection", techId);
            UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
        }
    }
}
