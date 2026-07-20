using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class CardBattle : CanvasLayer
    {
        /// <summary>Whose turn it is. Emitted with each turn change so card logic can hook in.</summary>
        [Signal] public delegate void TurnChangedEventHandler(bool playerTurn, int turnNumber);

        private Label? _banner;
        private Button? _endTurnButton;
        private bool _playerTurn = true;
        private int _turnNumber = 1;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            _banner = GetNodeOrNull<Label>("Center/Panel/Margin/VBox/BannerLabel");
            _endTurnButton = GetNodeOrNull<Button>("Center/Panel/Margin/VBox/ButtonRow/EndTurnButton");
            if (_endTurnButton != null) _endTurnButton.Pressed += OnEndTurn;
            this.ConnectPressed("Center/Panel/Margin/VBox/ButtonRow/ForfeitButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));

            UpdateBanner();
        }

        /// <summary>End the current side's turn. Player → opponent; the opponent takes its
        /// (stubbed-for-the-game-dev) action, then hands back with the turn counter advanced.
        /// The turn STATE machine is real and complete; the per-card actions are the game's.</summary>
        private void OnEndTurn()
        {
            if (!_playerTurn) return;

            // Advance the genre's turn clock. Durational effects (buffs, cooldowns, production)
            // decrement off TurnManager.TurnEnded; this is the one place that fires it, so they
            // all share a single edge instead of inventing their own. TurnChanged (below) stays
            // as this screen's UI signal.
            TurnManager.Instance?.EndTurn();

            _playerTurn = false;
            // Lock End Turn while the opponent "acts". Leaving it live let a second press
            // inside the 0.6s window run EndOpponentTurn immediately without cancelling the
            // pending timer — which then fired too, advancing the turn twice and emitting
            // TurnChanged twice for one hand-off.
            if (_endTurnButton != null) _endTurnButton.Disabled = true;
            UpdateBanner();
            EmitSignal(SignalName.TurnChanged, false, _turnNumber);
            // Hand back to the player after the opponent "acts". A game replaces this
            // timer with real opponent AI, but the turn hand-off itself works today.
            GetTree().CreateTimer(0.6).Timeout += EndOpponentTurn;
        }

        private void EndOpponentTurn()
        {
            // Belt-and-braces against a re-entrant hand-back (the timer surviving a
            // QueueFree'd button, a game calling this directly): only the opponent's turn
            // can hand back to the player.
            if (_playerTurn) return;

            _playerTurn = true;
            _turnNumber++;
            if (_endTurnButton != null) _endTurnButton.Disabled = false;
            UpdateBanner();
            EmitSignal(SignalName.TurnChanged, true, _turnNumber);
        }

        private void UpdateBanner()
        {
            if (_banner != null)
                _banner.Text = _playerTurn ? $"Your Turn  (Turn {_turnNumber})" : $"Opponent's Turn  (Turn {_turnNumber})";
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
