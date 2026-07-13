using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Power receiver component. Blind — receives power from any source.
    /// Works for lights, machines, doors, turrets, anything needing power.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PowerReceiver : GameplayComponent
    {
        [Export] public float PowerRequired { get; set; } = 50f;
        [Export] public float Efficiency { get; set; } = 1f;

        [Signal] public delegate void PowerReceivedEventHandler(float power, double delta);
        [Signal] public delegate void PowerCutEventHandler();
        [Signal] public delegate void PowerRestoredEventHandler();

        public bool IsPowered { get; private set; }

        public void ReceivePower(float amount, double delta)
        {
            float needed = PowerRequired * Efficiency;
            if (amount >= needed)
            {
                if (!IsPowered) { IsPowered = true; EmitSignal(SignalName.PowerRestored); }
                EmitSignal(SignalName.PowerReceived, amount, delta);
            }
            else if (IsPowered)
            {
                IsPowered = false;
                EmitSignal(SignalName.PowerCut);
            }
        }
    }
}
