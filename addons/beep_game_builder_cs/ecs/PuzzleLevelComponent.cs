using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The puzzle genre's level state: the score target, the move budget, and whether the level
    /// is won or lost.
    ///
    /// <c>PuzzleHudComponent</c> registered Target and Moves as <c>Placeholder(...)</c>, so both
    /// showed whatever text was typed into the scene. Sixth genre to get a real one.
    ///
    /// What makes it a puzzle level rather than two counters:
    ///  - the level ENDS. Running out of moves loses, hitting the target wins, and both are
    ///    resolved once rather than re-fired every time a move is spent
    ///  - the target is checked on every score change, so a chain reaction that crosses it wins
    ///    immediately instead of waiting for the next move
    ///  - moves remaining is spent through one method, so a level cannot go to -1 moves
    ///  - stars are earned against the target, which is what makes overshooting it worth doing
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PuzzleLevelComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int TargetScore { get; set; } = 1000;
        [Export] public int MoveBudget { get; set; } = 25;

        /// <summary>Score multiples of the target that earn the second and third star. A level
        /// worth only one star gives a player no reason to keep playing a solved board.</summary>
        [Export] public float TwoStarMultiple { get; set; } = 1.5f;
        [Export] public float ThreeStarMultiple { get; set; } = 2.0f;

        /// <summary>At or below this many moves the HUD warns.</summary>
        [Export] public int LowMovesThreshold { get; set; } = 5;

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool IsOver => Won || Lost;

        public bool IsLowOnMoves => !IsOver && MovesLeft <= LowMovesThreshold;
        public float TargetFraction => TargetScore <= 0 ? 1f
            : Mathf.Clamp((float)Score / TargetScore, 0f, 1f);
        public float MovesFraction => MoveBudget <= 0 ? 0f
            : Mathf.Clamp((float)MovesLeft / MoveBudget, 0f, 1f);

        /// <summary>0..3. Zero until the target is met, so a losing board never shows a star.</summary>
        public int Stars
        {
            get
            {
                if (TargetScore <= 0 || Score < TargetScore) return 0;
                if (Score >= TargetScore * ThreeStarMultiple) return 3;
                if (Score >= TargetScore * TwoStarMultiple) return 2;
                return 1;
            }
        }

        [Signal] public delegate void LevelChangedEventHandler();
        [Signal] public delegate void LevelWonEventHandler(int stars);
        [Signal] public delegate void LevelLostEventHandler();

        public override void _Ready()
        {
            base._Ready();
            MovesLeft = MoveBudget;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        // ── Level API ─────────────────────────────────────────────────────────────────────

        /// <summary>Award points. Checked against the target immediately, so a chain reaction
        /// that crosses it wins on the spot rather than waiting for the next move.</summary>
        public void AddScore(int points)
        {
            if (points <= 0 || IsOver) return;
            Score += points;
            EmitSignal(SignalName.LevelChanged);
            CheckWin();
        }

        /// <summary>Spend a move. Returns false when the level is over or the budget is gone, so
        /// a board can refuse input instead of going to -1 moves.</summary>
        public bool SpendMove(int count = 1)
        {
            if (IsOver || MovesLeft <= 0) return false;
            MovesLeft = Mathf.Max(0, MovesLeft - Mathf.Max(1, count));
            EmitSignal(SignalName.LevelChanged);

            // Win takes precedence: a move that reaches the target on the last move is a win,
            // not a loss. Checking loss first would steal it.
            if (CheckWin()) return true;
            if (MovesLeft <= 0)
            {
                Lost = true;
                EmitSignal(SignalName.LevelLost);
                EmitSignal(SignalName.LevelChanged);
            }
            return true;
        }

        /// <summary>Resolve a win once. Returns whether the level is now won.</summary>
        private bool CheckWin()
        {
            if (IsOver || TargetScore <= 0 || Score < TargetScore) return Won;
            Won = true;
            EmitSignal(SignalName.LevelWon, Stars);
            EmitSignal(SignalName.LevelChanged);
            return true;
        }

        /// <summary>Grant extra moves — a booster, or a reward for a chain.</summary>
        public void AddMoves(int count)
        {
            if (count <= 0 || IsOver) return;
            MovesLeft += count;
            EmitSignal(SignalName.LevelChanged);
        }

        /// <summary>Restart with the current tuning.</summary>
        public void RestartLevel()
        {
            Score = 0;
            MovesLeft = MoveBudget;
            Won = Lost = false;
            EmitSignal(SignalName.LevelChanged);
        }

        /// <summary>Load a different level's tuning and restart against it.</summary>
        public void BeginLevel(int targetScore, int moveBudget)
        {
            TargetScore = Mathf.Max(1, targetScore);
            MoveBudget = Mathf.Max(1, moveBudget);
            RestartLevel();
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KScore = "puzzle.score";
        private const string KMoves = "puzzle.moves_left";
        private const string KTarget = "puzzle.target";
        private const string KBudget = "puzzle.budget";
        private const string KWon = "puzzle.won";
        private const string KLost = "puzzle.lost";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KScore] = Score;
            state.GameData[KMoves] = MovesLeft;
            // The level's own tuning is saved too: a save restored against a DIFFERENT level's
            // target would show progress toward a goal the player never had.
            state.GameData[KTarget] = TargetScore;
            state.GameData[KBudget] = MoveBudget;
            state.GameData[KWon] = Won;
            state.GameData[KLost] = Lost;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KTarget, out var t)) TargetScore = Mathf.Max(1, t.AsInt32());
            if (d.TryGetValue(KBudget, out var b)) MoveBudget = Mathf.Max(1, b.AsInt32());
            if (d.TryGetValue(KScore, out var s)) Score = Mathf.Max(0, s.AsInt32());
            // Clamped AFTER the budget is restored, or a 40-move save would be capped by the
            // default 25.
            if (d.TryGetValue(KMoves, out var m)) MovesLeft = Mathf.Clamp(m.AsInt32(), 0, MoveBudget);
            if (d.TryGetValue(KWon, out var w)) Won = w.AsBool();
            if (d.TryGetValue(KLost, out var l)) Lost = l.AsBool();
            EmitSignal(SignalName.LevelChanged);
        }
    }
}
