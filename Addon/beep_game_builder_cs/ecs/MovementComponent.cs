using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Movement component for any entity that moves.
    /// Blind — works for player, NPC, projectile, or camera follow target.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MovementComponent : EntityComponent
    {
        [Export] public float Speed { get; set; } = 200f;
        [Export] public float Acceleration { get; set; } = 1800f;
        [Export] public float Friction { get; set; } = 1400f;
        [Export] public bool CanDash { get; set; } = false;
        [Export] public float DashSpeed { get; set; } = 600f;
        [Export] public float DashDuration { get; set; } = 0.15f;
        [Export] public float DashCooldown { get; set; } = 0.8f;

        [Signal] public delegate void MovedEventHandler(Vector2 direction, float speed);
        [Signal] public delegate void DashedEventHandler(Vector2 direction);
        [Signal] public delegate void StoppedEventHandler();

        public Vector2 Velocity { get; set; }
        public Vector2 DesiredDirection { get; set; }

        private float _dashTimer;
        private float _dashCooldownTimer;

        public void Move(Vector2 direction, double delta)
        {
            if (!IsActive) return;
            DesiredDirection = direction;

            if (_dashTimer > 0)
            {
                _dashTimer -= (float)delta;
                return;
            }

            _dashCooldownTimer = Mathf.Max(0, _dashCooldownTimer - (float)delta);

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

        public void Dash(Vector2 direction)
        {
            if (!CanDash || _dashCooldownTimer > 0 || !IsActive) return;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
            Velocity = direction * DashSpeed;
            EmitSignal(SignalName.Dashed, direction);
        }
    }
}
