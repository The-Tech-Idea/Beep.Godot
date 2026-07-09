using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Power source component. Blind — emits power for any receiver in range.
    /// Works for batteries, generators, engines, solar panels, magic crystals.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PowerSource : EntityComponent
    {
        [Export] public float PowerAmount { get; set; } = 100f;
        [Export] public float Efficiency { get; set; } = 1f;
        [Export] public float Range { get; set; } = 200f;

        [Signal] public delegate void PowerProvidedEventHandler(float power, double delta);
        [Signal] public delegate void PowerDrawnEventHandler(float amount, double delta);

        public float AvailablePower => PowerAmount * Efficiency;

        public void DrawPower(float amount, double delta)
        {
            EmitSignal(SignalName.PowerDrawn, amount, delta);
        }
    }

    /// <summary>
    /// Power receiver component. Blind — receives power from any source.
    /// Works for lights, machines, doors, turrets, anything needing power.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PowerReceiver : EntityComponent
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
