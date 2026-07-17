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
        private bool _playerTurn = true;
        private int _turnNumber = 1;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            _banner = GetNodeOrNull<Label>("Center/Panel/Margin/VBox/BannerLabel");
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/EndTurnButton").Pressed += OnEndTurn;
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/ForfeitButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);

            UpdateBanner();
        }

        /// <summary>End the current side's turn. Player → opponent; the opponent takes its
        /// (stubbed-for-the-game-dev) action, then hands back with the turn counter advanced.
        /// The turn STATE machine is real and complete; the per-card actions are the game's.</summary>
        private void OnEndTurn()
        {
            if (_playerTurn)
            {
                _playerTurn = false;
                UpdateBanner();
                EmitSignal(SignalName.TurnChanged, false, _turnNumber);
                // Hand back to the player after the opponent "acts". A game replaces this
                // timer with real opponent AI, but the turn hand-off itself works today.
                GetTree().CreateTimer(0.6).Timeout += EndOpponentTurn;
            }
            else
            {
                EndOpponentTurn();
            }
        }

        private void EndOpponentTurn()
        {
            _playerTurn = true;
            _turnNumber++;
            UpdateBanner();
            EmitSignal(SignalName.TurnChanged, true, _turnNumber);
        }

        private void UpdateBanner()
        {
            if (_banner != null)
                _banner.Text = _playerTurn ? $"Your Turn  (Turn {_turnNumber})" : $"Opponent's Turn  (Turn {_turnNumber})";
        }

        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.</summary>
        private void ChangeScene(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Navigation target does not exist: {path}");
                return;
            }
            Error err = GetTree().ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{Name}] Failed to change scene to {path}: {err}");
        }
    }
}
