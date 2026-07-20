using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The turn clock for turn-based genres (cardgame, strategy). Registered as an autoload by
    /// the generator ONLY when <c>genre.json tuning.time_axis == "turns"</c>; its very presence in
    /// the tree is the signal that durations tick per turn rather than per frame.
    ///
    /// It is deliberately tiny — a <a href="https://mwhittaker.github.io/blog/lamports_logical_clocks/">
    /// Lamport logical clock</a>: a counter and a signal, no wall time. There is no delta, no scale,
    /// no pause, no "now": those are meaningless for turns, and a single clock type that carried both
    /// them and turns would be a union interface whose implementers no-op on half of it. Real-time
    /// genres use <c>delta</c> and <c>Engine.time_scale</c> directly and never touch this. See phase-7.
    ///
    /// Duration convention (state it, don't infer it): a durational effect <b>decrements at the end
    /// of a turn and expires at 0</b> (Slay the Spire's rule), fired from exactly one place —
    /// <see cref="TurnEnded"/> — so every consumer shares one off-by-one instead of inventing N.
    /// </summary>
    [GlobalClass]
    public partial class TurnManager : Node
    {
        /// <summary>The autoload singleton, or null in a real-time genre (where it isn't registered).
        /// Consumers use <c>TurnManager.Instance != null</c> to detect the turn axis.</summary>
        public static TurnManager? Instance { get; private set; }

        /// <summary>Turns elapsed since the game began. Monotonic; only <see cref="EndTurn"/> advances it.</summary>
        public int CurrentTurn { get; private set; }

        /// <summary>Emitted once per <see cref="EndTurn"/>, carrying the new turn number. The single
        /// heartbeat durational components subscribe to.</summary>
        [Signal] public delegate void TurnEndedEventHandler(int turn);

        public override void _EnterTree()
        {
            // Autoloads are single-instance; guard anyway so a stray scene copy can't hijack the static.
            if (Instance != null && Instance != this)
            {
                GD.PushWarning($"[TurnManager] A second TurnManager entered the tree ('{Name}'); ignoring it. There must be exactly one (the autoload).");
                return;
            }
            Instance = this;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (Instance == this) Instance = null;
        }

        /// <summary>End the current turn: advance the counter and emit <see cref="TurnEnded"/>.
        /// That is the whole type.</summary>
        public void EndTurn()
        {
            CurrentTurn++;
            EmitSignal(SignalName.TurnEnded, CurrentTurn);
        }
    }
}
