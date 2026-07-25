using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Diplomacy : Control
    {
        /// <summary>GameData key holding the faction the player last inspected ("Faction1"…).
        /// Relations, treaties and AI attitude are the game's; this screen records the selection
        /// and drives the detail pane, the same choice-record pattern as Research / BuildMenu.</summary>
        public const string SelectionKey = "diplomacy_selection";

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectButton("BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));

            // The faction rail is made of real buttons, so each one must act. Selecting fills the
            // detail pane on the right — which is otherwise unreachable and looks broken.
            WireFaction("Faction1");
            WireFaction("Faction2");
            WireFaction("Faction3");
        }

        private void WireFaction(string buttonName)
        {
            if (this.Find<Button>(buttonName) is not { } btn) return;
            btn.Pressed += () =>
            {
                GameStateManagerComponent.Instance?.SetGameData(SelectionKey, buttonName);
                if (this.Find<Label>("FactionTitle") is { } title) title.Text = btn.Text;
            };
        }
    }
}
