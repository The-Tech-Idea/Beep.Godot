using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelUpChoice : CanvasLayer
    {
        /// <summary>GameData key holding the chosen upgrade's action id ("pick_1"…).
        /// Applying the upgrade is the game's job — this screen only records the choice.</summary>
        public const string PickKey = "levelup_pick";

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // All three handlers used to be byte-identical, so the choice was discarded —
            // the same bug CharacterSelect and VehicleSelect document as fixed on their side.
            // Each card carries its own metadata/action ("pick_1".."pick_3"); read it rather
            // than hardcoding, so re-ordering the cards in the scene can't desync the picks.
            WirePick("Center/VBox/CardRow/Card1/Card1VBox/Pick1");
            WirePick("Center/VBox/CardRow/Card2/Card2VBox/Pick2");
            WirePick("Center/VBox/CardRow/Card3/Card3VBox/Pick3");
        }

        private void WirePick(string nodePath)
        {
            if (GetNodeOrNull<Button>(nodePath) is not { } button) return;

            string action = button.HasMeta("action") ? button.GetMeta("action").AsString() : "";
            if (string.IsNullOrEmpty(action))
            {
                GD.PushWarning($"[{Name}] {nodePath} has no metadata/action — its pick cannot be recorded.");
                return;
            }

            button.Pressed += () => OnPicked(action);
        }

        private void OnPicked(string action)
        {
            GameStateManagerComponent.Instance?.SetGameData(PickKey, action);

            // Dismiss, don't navigate. This is an overlay (CanvasLayer layer=30 over a dim);
            // ChangeScene(GameScenePath) reloaded the game scene underneath it, so levelling
            // up restarted the run — the hazard SettingsOverlay exists to avoid.
            QueueFree();
        }
    }
}
