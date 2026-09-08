using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The one node in the grid toolkit that knows what a unit of work is, and
    /// the only seam between the grid and the game clock.
    ///
    /// THE UNIT IS THE TURN, and a turn is a day. A build that declares 5 turns
    /// takes five end-turns in a turn-based game and five in-game days in a
    /// real-time one - the same authored number, the same amount of world time,
    /// on both axes. That is what makes one number meaningful in both modes:
    /// the game clock counts beats and cascades them into days, and this
    /// component divides by that cascade so the grid always measures in days.
    /// Forwarding beats straight through would make "5 turns" mean five seconds
    /// in an RTS.
    ///
    /// It is found by NAME, never by type - see GridClockPorts - so the grid
    /// keeps depending on no global. With no clock anywhere it drives itself in
    /// real time at SecondsPerTurn, which is what lets a template scene open on
    /// its own and every headless probe run without standing up a game.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkClockComponent : Node
    {
        private static readonly StringName ClockGroup = new("_beep_grid_work_clocks");

        public override void _EnterTree() => AddToGroup(ClockGroup);

        internal static GridWorkClockComponent? FindFor(Node owner, NodePath path)
        {
            if (!path.IsEmpty) return owner.GetNodeOrNull<Node>(path) as GridWorkClockComponent;
            if (!owner.IsInsideTree() || owner.GetTree().CurrentScene is not { } scene) return null;
            foreach (Node candidate in owner.GetTree().GetNodesInGroup(ClockGroup))
                if (candidate is GridWorkClockComponent clock && (clock == scene || scene.IsAncestorOf(clock)))
                    return clock;
            return null;
        }
        /// <summary>A unit of work happened. Carries turns elapsed - 1.0 is a whole turn.</summary>
        [Signal] public delegate void WorkTickEventHandler(float turns);

        /// <summary>The calendar this drives, if the scene has one. Empty finds it scene-wide.</summary>
        [Export] public NodePath CalendarPath { get; set; } = new("");

        /// <summary>
        /// Seconds per turn when there is no game clock at all. Only the
        /// standalone case uses this; a real game gets its pacing from the
        /// clock's own BeatsPerDay.
        /// </summary>
        [Export(PropertyHint.Range, "0.01,3600,0.01")] public float SecondsPerTurn { get; set; } = 1f;

        /// <summary>True when a game clock is driving this; false when self-ticking.</summary>
        public bool FollowsGameClock => _clock != null && GodotObject.IsInstanceValid(_clock);

        /// <summary>Turns elapsed since the scene started.</summary>
        public double ElapsedTurns { get; private set; }

        /// <summary>
        /// How far through the current day, 0..1 - what a day-progress bar
        /// reads. Comes from the game clock when there is one, so the bar and
        /// the calendar can never disagree about the same fraction.
        /// </summary>
        public float DayProgress01 => FollowsGameClock
            ? Mathf.Clamp((float)_clock!.Get(GridClockPorts.DayProgressProperty).AsDouble(), 0f, 1f)
            : Mathf.Clamp((float)_turnFraction, 0f, 1f);

        private Node? _clock;
        private GridCalendarComponent? _calendar;
        private double _turnFraction;
        private bool _connected;

        // Built once and kept: Callable.From wraps a FRESH delegate every call,
        // so disconnecting with a newly-built one would not match what was
        // connected and the handler would outlive this node.
        private Callable? _advancedCallable;
        private Callable? _dayAdvancedCallable;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
            {
                SetProcess(false);
                return;
            }

            ResolveReferences();
            BindClock();
            GetTree().NodeRemoved += CancelOwnerWork;
            // Self-tick only when nothing else will.
            SetProcess(!FollowsGameClock);
        }

        public override void _ExitTree()
        {
            if (!Engine.IsEditorHint()) GetTree().NodeRemoved -= CancelOwnerWork;
            ClearScheduledWork();
            UnbindClock();
            RemoveFromGroup(ClockGroup);
            RequestReady();
        }

        public override void _Process(double delta)
        {
            // Reached only in the no-clock standalone case.
            if (Engine.IsEditorHint() || !double.IsFinite(delta) || delta <= 0.0)
                return;

            float seconds = Mathf.Max(0.01f, SecondsPerTurn);
            EmitWork((float)(Mathf.Min(delta, 86400.0) / seconds));
        }

        /// <summary>
        /// Advances work deliberately, in turns. The way a test or a tool steps
        /// the grid without caring which axis the game is on.
        /// </summary>
        public void AdvanceTurns(float turns)
        {
            if (!float.IsFinite(turns) || turns <= 0f)
                return;

            EmitWork(turns);
        }

        private void EmitWork(float turns)
        {
            ElapsedTurns += turns;
            EmitSignal(SignalName.WorkTick, turns);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
            DispatchScheduledWork();
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;

            // With a game clock, IT owns the day and its DayAdvanced drives the
            // calendar - accumulating here as well would advance the date twice.
            if (FollowsGameClock)
                return;

            _turnFraction += turns;
            while (_turnFraction >= 1.0)
            {
                _turnFraction -= 1.0;
                ResolveReferences();
                _calendar?.AdvanceDay();
            }
        }

        private void OnClockAdvanced(double beats)
        {
            if (!double.IsFinite(beats) || beats <= 0.0)
                return;

            // Beats into days: the grid's unit is the day, whatever the axis.
            if (_clock != null && GodotObject.IsInstanceValid(_clock))
                EmitWork((float)(beats / GridClockPorts.BeatsPerDay(_clock)));
        }

        // The calendar derives from the clock and never runs one of its own -
        // Freeciv advances the year inside end_turn for the same reason.
        private void OnClockDayAdvanced(int day)
        {
            ResolveReferences();
            _calendar?.AdvanceDay();
        }

        private void BindClock()
        {
            _clock = GridClockPorts.FindClock(GetTree());
            if (_clock == null)
                return;

            _advancedCallable = Callable.From<double>(OnClockAdvanced);
            _dayAdvancedCallable = Callable.From<int>(OnClockDayAdvanced);
            _clock.Connect(GridClockPorts.AdvancedSignal, _advancedCallable.Value);
            _clock.Connect(GridClockPorts.DayAdvancedSignal, _dayAdvancedCallable.Value);
            _connected = true;
        }

        private void UnbindClock()
        {
            if (_connected && _clock != null && GodotObject.IsInstanceValid(_clock)
                && _advancedCallable.HasValue && _dayAdvancedCallable.HasValue)
            {
                _clock.Disconnect(GridClockPorts.AdvancedSignal, _advancedCallable.Value);
                _clock.Disconnect(GridClockPorts.DayAdvancedSignal, _dayAdvancedCallable.Value);
            }

            _advancedCallable = null;
            _dayAdvancedCallable = null;
            _connected = false;
        }

        private void ResolveReferences()
            => EntityComponent.Resolve(this, CalendarPath, ref _calendar);
    }
}
