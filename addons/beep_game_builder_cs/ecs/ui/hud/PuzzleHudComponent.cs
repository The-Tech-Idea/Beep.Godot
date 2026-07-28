using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Puzzle HUD: centred Score, plus the level Target and remaining Moves.
    ///
    /// Driven by <see cref="PuzzleLevelComponent"/>. Target and Moves were previously registered
    /// as <c>Placeholder(...)</c>, so both showed whatever text was typed into the scene.
    /// Placeholder is now the FALLBACK for a scene with no level component, not the normal path.
    ///
    /// Score still falls back to <c>BindScore</c> (GameApp) when there is no level component.</summary>
    [Tool]
    [GlobalClass]
    public partial class PuzzleHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopCenter/ScoreLabel";
        [Export] public NodePath TargetPath { get; set; } = "TopCenter/TargetLabel";
        [Export] public NodePath MovesPath { get; set; } = "TopCenter/MovesLabel";

        /// <summary>Optional toast host for win/lose alerts.</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "puzzle";

        private PuzzleLevelComponent? _level;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _score, _target, _moves;

        protected override void Wire()
        {
            _level = FindInScene<PuzzleLevelComponent>();

            if (_level == null)
            {
                // No simulation in this scene: keep the GameApp-bound score and fall back to
                // developer-driven readouts. This is the only path that should ever warn.
                BindScore(ScorePath);
                Placeholder(TargetPath, "target");
                Placeholder(MovesPath, "moves");
                return;
            }

            _score = ResolveReadout(ScorePath, "score");
            _target = ResolveReadout(TargetPath, "target");
            _moves = ResolveReadout(MovesPath, "moves");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _level.LevelChanged += OnLevel;
            _level.LevelWon += OnWon;
            _level.LevelLost += OnLost;
            OnLevel();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_level != null && GodotObject.IsInstanceValid(_level))
            {
                _level.LevelChanged -= OnLevel;
                _level.LevelWon -= OnWon;
                _level.LevelLost -= OnLost;
            }
            _level = null;
        }

        private void OnLevel()
        {
            if (_level == null) return;

            SetReadout(_score, _level.Score.ToString("N0"));

            // The target readout carries progress toward it as its fill, which is the one number
            // a puzzle player is actually tracking.
            SetReadout(_target, $"{_level.Score:N0} / {_level.TargetScore:N0}", _level.TargetFraction);
            Tint(_target, _level.Won ? UiSurface.Role.Success : null);

            SetReadout(_moves, $"{_level.MovesLeft} moves", _level.MovesFraction);
            Tint(_moves, _level.Lost ? UiSurface.Role.Danger
                 : _level.IsLowOnMoves ? UiSurface.Role.Warning
                 : null);
        }

        private void OnWon(int stars)
            => _alerts?.ShowToast(stars > 0 ? $"Level complete  {new string('★', stars)}"
                                            : "Level complete",
                                  ToastNotificationComponent.ToastType.Success);

        private void OnLost()
            => _alerts?.ShowToast("Out of moves", ToastNotificationComponent.ToastType.Error);
    }
}
