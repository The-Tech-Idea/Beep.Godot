using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The game's one heartbeat, owned by <see cref="GameApp"/>. Every durational
    /// system in the addon advances off this and nothing else.
    ///
    /// ONE CLOCK, TWO DRIVERS. A real-time game and a turn-based game are not
    /// different kinds of time - they are the same discrete step with different
    /// triggers. Age of Empires' lockstep simulation, Return to the Roots'
    /// RunGF() and Widelands' command queue all advance a real-time world in
    /// discrete steps; a turn-based game advances the same step on a button.
    /// So the axis decides only WHO calls <see cref="Advance"/>: this node's own
    /// _Process on <see cref="GameTimeAxis.Realtime"/>, or
    /// <see cref="EndTurn"/> on <see cref="GameTimeAxis.Turns"/>. The _Process
    /// below is the only place in the addon that branches on the axis at all -
    /// consumers subscribe to <see cref="Advanced"/> and never ask which axis
    /// they are on.
    ///
    /// THE UNIT IS THE BEAT. One beat is one second of scaled game time on the
    /// real-time axis and one turn on the turn axis. Because a game has exactly
    /// one axis, a duration needs no unit tag: "5" means five seconds or five
    /// turns depending on the game it is in, and the same authored number works
    /// in both.
    ///
    /// LARGER UNITS CASCADE, they are not separate clocks. <see cref="Day"/>
    /// derives from beats through <see cref="BeatsPerDay"/> the way OpenTTD's
    /// date derives from date_fract, and the way Freeciv advances the calendar
    /// year inside end_turn rather than running a second clock beside it. A
    /// turn-based game sets BeatsPerDay = 1, so one end-turn is one day; a
    /// real-time game sets it high, so a day passes smoothly.
    ///
    /// PAUSE IS THE TREE'S, not this node's. SceneTree.Paused is the one pause
    /// fact in the game and GameApp.SetPaused is the one door to it. It already
    /// stops this node's _Process on the real-time axis, because this node
    /// inherits the default pausable process mode; on the turn axis
    /// <see cref="EndTurn"/> reads the same flag, so a turn cannot be ended
    /// from under the pause menu. A second pause flag here would be a second
    /// owner, and it was: it existed, nothing set it, and turns went through.
    /// </summary>
    [GlobalClass]
    public partial class GameClock : Node
    {
        /// <summary>The heartbeat. Carries how many beats just elapsed - a frame's
        /// worth on the real-time axis, exactly 1 on the turn axis.</summary>
        [Signal] public delegate void AdvancedEventHandler(double beats);

        /// <summary>A whole day has rolled over. Carries the new absolute day.</summary>
        [Signal] public delegate void DayAdvancedEventHandler(int day);

        /// <summary>A turn has ended. Turn axis only; carries the new turn number.</summary>
        [Signal] public delegate void TurnEndedEventHandler(int turn);

        public GameTimeAxis Axis { get; private set; } = GameTimeAxis.Realtime;
        public bool SimulationEnabled { get; set; } = true;

        /// <summary>Beats elapsed since the game began. Monotonic.</summary>
        public double Elapsed { get; private set; }

        /// <summary>Turns elapsed. Only <see cref="EndTurn"/> advances it, so it stays 0 on the real-time axis.</summary>
        public int Turn { get; private set; }

        /// <summary>Whole days elapsed, cascaded from beats. Both axes.</summary>
        public int Day { get; private set; }

        /// <summary>
        /// Beats in one day - the cascade divisor. 1 on the turn axis (one turn
        /// is one day); the real-time equivalent of a day length in seconds.
        /// </summary>
        public double BeatsPerDay { get; private set; } = 1.0;

        /// <summary>How far through the current day, 0..1. Drives day-progress HUD.</summary>
        public double DayProgress01 => BeatsPerDay > 0.0 ? Mathf.Clamp(_dayFraction / BeatsPerDay, 0.0, 1.0) : 0.0;

        /// <summary>
        /// Beats into the current day, 0 to BeatsPerDay. The third number a save
        /// needs beside Elapsed and Turn: Day itself is never stored, it is
        /// re-derived from these three on <see cref="RestoreState"/>.
        /// </summary>
        public double DayFraction => _dayFraction;

        private float _scale = 1f;

        /// <summary>
        /// Game speed on the real-time axis. Meaningless on the turn axis - a
        /// turn is a turn - so setting it there is reported rather than
        /// silently ignored.
        /// </summary>
        public float Scale
        {
            get => _scale;
            set
            {
                if (Axis == GameTimeAxis.Turns && !Mathf.IsEqualApprox(value, 1f))
                {
                    GD.PushWarning($"[{Name}] Scale has no meaning on the turn axis - a turn is a turn. Ignoring {value}.");
                    return;
                }
                _scale = float.IsFinite(value) ? Mathf.Max(0f, value) : 1f;
            }
        }

        private double _dayFraction;

        /// <summary>
        /// Declares the axis and the cascade. Called by GameApp.ReconfigureClock
        /// from the genre's own GameInfo.TimeAxis - once when the master builds
        /// its subsystems, and again whenever something rewrites GameInfo after
        /// that (a BeepGenreScene applying its genre's tuning). Nothing infers
        /// either value.
        ///
        /// Elapsed is kept; Day and the day fraction are re-derived from it, so
        /// the invariant Elapsed = Day x BeatsPerDay + DayFraction holds under
        /// the new divisor instead of the old remainder cascading as a burst of
        /// spurious days on the next beat. No DayAdvanced is emitted for the
        /// re-derivation: nothing happened in the game, only the unit changed.
        /// </summary>
        public void Configure(GameTimeAxis axis, double beatsPerDay)
        {
            Axis = axis;
            BeatsPerDay = double.IsFinite(beatsPerDay) && beatsPerDay > 0.0 ? beatsPerDay : 1.0;
            if (axis == GameTimeAxis.Turns)
                _scale = 1f;
            Day = (int)(Elapsed / BeatsPerDay);
            _dayFraction = Elapsed - Day * BeatsPerDay;
            SetProcess(axis == GameTimeAxis.Realtime);
        }

        /// <summary>
        /// Advances the clock and everything hanging off it. Public so a test,
        /// a cutscene or a debug tool can step time deliberately on either axis.
        /// A non-finite or non-positive amount is ignored rather than corrupting
        /// the counters; a single huge step is clamped the way every other
        /// delta consumer in this addon clamps one.
        /// </summary>
        public void Advance(double beats)
        {
            if (!double.IsFinite(beats) || beats <= 0.0)
                return;

            double step = Mathf.Min(beats, 86400.0);
            Elapsed += step;
            EmitSignal(SignalName.Advanced, step);

            _dayFraction += step;
            while (_dayFraction >= BeatsPerDay && BeatsPerDay > 0.0)
            {
                _dayFraction -= BeatsPerDay;
                Day++;
                EmitSignal(SignalName.DayAdvanced, Day);
            }
        }

        /// <summary>
        /// Ends the current turn: one beat passes and everything durational
        /// advances by exactly one. Reports false when the caller asked for
        /// something this clock cannot do - the real-time axis has no turns, and
        /// a paused game advances for nobody - rather than appearing to work.
        ///
        /// The pause it honours is the tree's. On the real-time axis the tree
        /// pause stops _Process for free; the turn axis has no _Process, so the
        /// button that ends a turn would otherwise work straight through the
        /// pause menu.
        /// </summary>
        public bool EndTurn()
        {
            if (!SimulationEnabled) return false;
            if (Axis != GameTimeAxis.Turns)
            {
                GD.PushWarning($"[{Name}] EndTurn() on a real-time game. The axis is declared in GameInfo.TimeAxis.");
                return false;
            }

            if (IsInsideTree() && GetTree().Paused)
                return false;

            Turn++;
            // The beat lands BEFORE TurnEnded is announced, so a listener that
            // reads Day or Elapsed on that signal sees the turn it is being
            // told about, not the one before it.
            Advance(1.0);
            EmitSignal(SignalName.TurnEnded, Turn);
            return true;
        }

        /// <summary>
        /// Restores a saved clock from the three facts GameApp persists in
        /// SessionStateData. Day is re-derived, never stored twice. Runs after
        /// Configure - BuildSubsystems precedes any load - so BeatsPerDay is the
        /// right divisor when Day is worked out.
        /// </summary>
        public void RestoreState(double elapsed, int turn, double dayFraction)
        {
            Elapsed = double.IsFinite(elapsed) && elapsed > 0.0 ? elapsed : 0.0;
            Turn = Mathf.Max(0, turn);
            _dayFraction = double.IsFinite(dayFraction) && dayFraction > 0.0 ? dayFraction : 0.0;
            Day = BeatsPerDay > 0.0 ? (int)((Elapsed - _dayFraction) / BeatsPerDay) : 0;
        }

        // The one axis branch in the addon. On the turn axis this node does not
        // process at all - Configure turned it off - and EndTurn is the driver.
        // A paused tree never reaches here: this node is pausable.
        public override void _Process(double delta)
        {
            if (!SimulationEnabled || Axis != GameTimeAxis.Realtime)
                return;

            Advance(delta * _scale);
        }
    }
}
