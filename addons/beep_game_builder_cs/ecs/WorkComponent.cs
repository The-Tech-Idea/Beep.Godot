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

        public float Progress => TotalWorkRequired > 0 ? AvailableWork / TotalWorkRequired : 0f;
        public bool IsWorking { get; private set; }

        public void StartWork(float workUnits)
        {
            if (!IsActive) return;
            AvailableWork = workUnits;
            TotalWorkRequired = workUnits;
            IsWorking = true;
            EmitSignal(SignalName.WorkStarted);
        }

        public void Tick(double delta)
        {
            if (!IsWorking || !IsActive) return;
            float done = (float)delta * WorkSpeed;
            AvailableWork -= done;
            EmitSignal(SignalName.WorkAccomplished, done, Progress);

            if (AvailableWork <= 0f)
            {
                EmitSignal(SignalName.WorkDone, OutputItemId, OutputQuantity);
                if (LoopProduction)
                    AvailableWork = TotalWorkRequired;
                else
                { IsWorking = false; EmitSignal(SignalName.WorkStopped); }
            }
        }
    }
}
