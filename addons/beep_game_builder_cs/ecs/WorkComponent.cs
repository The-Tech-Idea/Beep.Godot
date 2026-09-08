using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Work/Crafting component. Blind — works for furnaces, factories, workbenches, labs, kitchens.
    /// Progresses through work units and emits output on completion.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WorkComponent : GameplayComponent
    {
        [Export] public float AvailableWork { get; set; } = 0f;
        [Export] public float WorkSpeed { get; set; } = 10f;
        [Export] public string OutputItemId { get; set; } = "";
        [Export] public int OutputQuantity { get; set; } = 1;
        [Export] public bool LoopProduction { get; set; } = false;
        [Export] public float TotalWorkRequired { get; set; } = 100f;

        [Signal] public delegate void WorkAccomplishedEventHandler(float amount, float progress);
        [Signal] public delegate void WorkDoneEventHandler(string outputItem, int quantity);
        [Signal] public delegate void WorkStartedEventHandler();
        [Signal] public delegate void WorkStoppedEventHandler();

        /// <summary>Completion fraction 0→1. AvailableWork counts DOWN from TotalWorkRequired, so
        /// progress is what has been done: 1 − remaining/total. (It used to return the remaining
        /// fraction — 1.0 at the start, draining to 0 — which read backwards on any progress bar.)</summary>
        public float Progress => EffectiveTotalWorkRequired > 0f
            ? Mathf.Clamp(1f - EffectiveAvailableWork / EffectiveTotalWorkRequired, 0f, 1f)
            : 0f;
        public bool IsWorking { get; private set; }
        public float EffectiveAvailableWork => NonNegative(AvailableWork);
        public float EffectiveWorkSpeed => NonNegative(WorkSpeed);
        public int EffectiveOutputQuantity => Mathf.Max(1, OutputQuantity);
        public float EffectiveTotalWorkRequired => Mathf.Max(0.001f, float.IsFinite(TotalWorkRequired) ? TotalWorkRequired : 100f);

        // The game clock drives this producer. It does NOT ask which time axis it
        // is on: one beat is one second in a real-time game and one turn in a
        // turn-based one, so the same Tick(beats) is correct on both. Asking used
        // to mean inferring the axis from whether a TurnManager node existed,
        // which is how a genre that declared turns but shipped no driver froze
        // every producer in the game with nothing to report it.
        private GameClock? _clock;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _clock = GameApp.Instance?.Clock;
            if (_clock != null)
                _clock.Advanced += OnAdvanced;

            // No GameApp at all means a bare scene - a template opened on its own,
            // or a headless probe - where no axis has been declared by anything.
            // Self-tick in real time so those keep working; a real game always has
            // the clock.
            SetProcess(_clock == null);
        }

        private void OnAdvanced(double beats) => Tick(beats);

        public override void _Process(double delta)
        {
            // Only reached in the no-clock standalone case; SetProcess is off otherwise.
            if (Engine.IsEditorHint()) return;
            Tick(delta);
        }

        public override void _ExitTree()
        {
            if (_clock != null && GodotObject.IsInstanceValid(_clock))
                _clock.Advanced -= OnAdvanced;
            _clock = null;
            base._ExitTree();
        }

        public void StartWork(float workUnits)
        {
            if (!IsActive) return;
            float effectiveWorkUnits = Mathf.Max(0.001f, float.IsFinite(workUnits) ? workUnits : EffectiveTotalWorkRequired);
            AvailableWork = effectiveWorkUnits;
            TotalWorkRequired = effectiveWorkUnits;
            IsWorking = true;
            EmitSignal(SignalName.WorkStarted);
        }

        public void Tick(double delta)
        {
            if (!IsWorking || !IsActive) return;
            AvailableWork = EffectiveAvailableWork;
            float done = DeltaSeconds(delta) * EffectiveWorkSpeed;
            AvailableWork = Mathf.Max(0f, AvailableWork - done);
            EmitSignal(SignalName.WorkAccomplished, done, Progress);

            if (AvailableWork <= 0f)
            {
                EmitSignal(SignalName.WorkDone, OutputItemId, EffectiveOutputQuantity);
                if (LoopProduction)
                {
                    // Re-arm for the next cycle AND re-announce it — a looping producer used to
                    // emit WorkStarted once and WorkStopped never, so a listener saw one start
                    // and then silent cycles forever.
                    AvailableWork = EffectiveTotalWorkRequired;
                    EmitSignal(SignalName.WorkStarted);
                }
                else
                { IsWorking = false; EmitSignal(SignalName.WorkStopped); }
            }
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
