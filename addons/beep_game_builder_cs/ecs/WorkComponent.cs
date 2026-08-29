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

        // Injectable clock. Tick(delta) was always the right signature — it just never had a
        // driver. Real-time genres tick it per frame; turn-based genres tick it once per turn.
        // The genre's axis is read from whether a TurnManager autoload is in the tree.
        private bool _turnBased;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _turnBased = TurnManager.Instance != null && GodotObject.IsInstanceValid(TurnManager.Instance);
            if (_turnBased)
                TurnManager.Instance!.TurnEnded += OnTurnEnded;
            else if (Beep.GameBuilder.GameInfo.Instance?.TimeAxis == "turns")
                GD.PushWarning(
                    $"[{Name}] TimeAxis is 'turns' but no TurnManager is in the tree — this producer " +
                    "will never tick and its work will never finish. Ensure the TurnManager autoload is registered.");
        }

        public override void _Process(double delta)
        {
            // Real-time only; in a turn-based genre the tick arrives from TurnEnded, once per turn.
            if (Engine.IsEditorHint() || _turnBased) return;
            Tick(delta);
        }

        private void OnTurnEnded(int turn) => Tick(1);

        public override void _ExitTree()
        {
            if (_turnBased && TurnManager.Instance != null && GodotObject.IsInstanceValid(TurnManager.Instance))
                TurnManager.Instance.TurnEnded -= OnTurnEnded;
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
