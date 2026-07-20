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

            this.ConnectPressed("Margin/VBox/Header/StartBattleButton", OnStartBattle);
        }

        private void OnStartBattle()
        {
            // Deck builder is an overlay over cardgame_main; "Start Battle" leaves it for the
            // card_battle screen (registered under the "card_battle" nav key). Nothing opened
            // card_battle before — StartBattle just closed the overlay, so the battle scene
            // shipped unreachable.
            string battle = Beep.GameBuilder.GameInfo.Instance?.GetGenreScenePath("card_battle") ?? "";
            if (string.IsNullOrEmpty(battle))
            {
                // card_battle not wired (e.g. running pre-generation) — fall back to closing.
                UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath);
                return;
            }
            // The genre-screen overlay paused the tree; the battle must run, so unpause first.
            var tree = GetTree();
            if (tree != null) tree.Paused = false;
            UI.SceneNav.ChangeScene(this, battle);
        }
    }
}
