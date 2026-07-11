using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Platformer movement controller component. Attach to any CharacterBody2D.
    /// Blind — works for players, enemies, NPCs with platformer physics.
    /// Uses Input actions: move_left, move_right, jump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PlatformerController : EntityComponent
    {
        [Export] public float Speed { get; set; } = 300f;
        [Export] public float JumpVelocity { get; set; } = -450f;
        [Export] public float Acceleration { get; set; } = 1200f;
        [Export] public float Friction { get; set; } = 1000f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public float JumpBufferTime { get; set; } = 0.1f;

        [Signal] public delegate void JumpedEventHandler();
        [Signal] public delegate void LandedEventHandler();
        [Signal] public delegate void MovedEventHandler(Vector2 direction);

        private CharacterBody2D? _body;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _wasInAir;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent<CharacterBody2D>();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;

            float dt = (float)delta;
            var input = Input.GetAxis("move_left", "move_right");
            bool onFloor = _body.IsOnFloor();

            // Coyote time
            if (onFloor) _coyoteTimer = CoyoteTime;
            else _coyoteTimer -= dt;

            // Jump buffer
            if (Input.IsActionJustPressed("jump")) _jumpBufferTimer = JumpBufferTime;
            else _jumpBufferTimer -= dt;

            // Jump
            if (_jumpBufferTimer > 0 && _coyoteTimer > 0)
            {
                _body.Velocity = new Vector2(_body.Velocity.X, JumpVelocity);
                _jumpBufferTimer = 0; _coyoteTimer = 0;
                EmitSignal(SignalName.Jumped);
            }

            // Horizontal movement
            float targetX = input * Speed;
            _body.Velocity = new Vector2(
                Mathf.MoveToward(_body.Velocity.X, targetX,
                    (input != 0 ? Acceleration : Friction) * dt),
                _body.Velocity.Y);
            _body.MoveAndSlide();

            if (input != 0) EmitSignal(SignalName.Moved, new Vector2(input, 0));

            // Land detection
            if (onFloor && _wasInAir) EmitSignal(SignalName.Landed);
            _wasInAir = !onFloor;
        }
    }
}
