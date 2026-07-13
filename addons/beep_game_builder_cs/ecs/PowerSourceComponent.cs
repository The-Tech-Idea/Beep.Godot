using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Power source component. Blind — emits power for any receiver in range.
    /// Works for batteries, generators, engines, solar panels, magic crystals.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PowerSource : GameplayComponent
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
}
