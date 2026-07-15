using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Free-flight movement component for top-down shooters, flying enemies, or
    /// zero-gravity sections. Attach to a CharacterBody2D. Provides full 360-degree
    /// movement with acceleration, friction, and optional banking (visual tilt
    /// based on turn direction).
    ///
    /// Composable — can replace TopDownController or ShooterController for flying
    /// sections, or stack alongside Dash for air-dash.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlyComponent : ControllerComponent
    {
        [ExportGroup("Flight")]
        [Export] public float MaxSpeed { get; set; } = 400f;
        [Export] public float Acceleration { get; set; } = 800f;
        [Export] public float Friction { get; set; } = 400f;
        [Export] public float TurnSpeed { get; set; } = 8f;

        [ExportGroup("Banking")]
        [Export] public bool EnableBanking { get; set; } = true;
        [Export] public float MaxBankAngle { get; set; } = 30f;
        [Export] public float BankSpeed { get; set; } = 5f;

        [Signal] public delegate void MovedEventHandler(Vector2 velocity);

        private CharacterBody2D? _body;
        private float _targetRotation;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            // Read 8-directional input.
            float x = Input.GetAxis("move_left", "move_right");
            float y = Input.GetAxis("move_up", "move_down");
            Vector2 inputDir = new(x, y);

            if (inputDir.Length() > 0)
            {
                inputDir = inputDir.Normalized();
                _body.Velocity = _body.Velocity.MoveToward(inputDir * MaxSpeed, Acceleration * dt);
                _targetRotation = inputDir.Angle();
            }
            else
            {
                _body.Velocity = _body.Velocity.MoveToward(Vector2.Zero, Friction * dt);
            }

            // Banking — smoothly rotate the body toward the movement direction.
            if (EnableBanking)
            {
                float bank = Mathf.LerpAngle(_body.Rotation, _targetRotation, TurnSpeed * dt);
                _body.Rotation = bank;
            }

            _body.MoveAndSlide();

            if (_body.Velocity.Length() > 1f)
                EmitSignal(SignalName.Moved, _body.Velocity);
        }

        /// <summary>Set velocity directly (for knockback, wind, conveyor belts).</summary>
        public void ApplyExternalForce(Vector2 force) { if (_body != null) _body.Velocity += force; }
    }
}
