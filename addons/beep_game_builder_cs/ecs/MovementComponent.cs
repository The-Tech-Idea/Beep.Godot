using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Movement component for any entity that moves.
    /// Blind — works for player, NPC, projectile, or camera follow target.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MovementComponent : GameplayComponent
    {
        [Export] public float Speed { get; set; } = 200f;
        [Export] public float Acceleration { get; set; } = 1800f;
        [Export] public float Friction { get; set; } = 1400f;

        // Dash lived here AND in the dedicated DashComponent (which also has i-frames +
        // afterimage + input handling). Removed from here — use DashComponent for dashing.

        [Signal] public delegate void MovedEventHandler(Vector2 direction, float speed);
        [Signal] public delegate void StoppedEventHandler();

        public Vector2 Velocity { get; set; }
        public Vector2 DesiredDirection { get; set; }

        public void Move(Vector2 direction, double delta)
        {
            if (!IsActive) return;
            DesiredDirection = direction;

            if (direction.Length() > 0)
            {
                Velocity = Velocity.MoveToward(direction * Speed, Acceleration * (float)delta);
                EmitSignal(SignalName.Moved, direction, Speed);
            }
            else
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
                if (Velocity.Length() < 1f) EmitSignal(SignalName.Stopped);
            }
        }
    }
}
