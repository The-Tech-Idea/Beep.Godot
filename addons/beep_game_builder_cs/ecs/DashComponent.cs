using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Dash ability component. Attach to a CharacterBody2D. Provides a burst-speed
    /// directional dash with cooldown, optional i-frames (invincibility), and an
    /// afterimage trail effect.
    ///
    /// Input action: "dash" (defaults to Shift). Dash direction follows current
    /// input or facing direction if no input is held.
    ///
    /// Composable — stack alongside Jump, Slide, Glide, Hover, WallJump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DashComponent : ControllerComponent
    {
        [ExportGroup("Dash")]
        [Export] public float DashSpeed { get; set; } = 800f;
        [Export] public float DashDuration { get; set; } = 0.15f;
        [Export] public float DashCooldown { get; set; } = 0.6f;
        [Export] public string DashAction { get; set; } = "dash";

        [ExportGroup("Invincibility")]
        [Export] public bool GrantIFrames { get; set; } = true;
        [Export] public float IFrameMultiplier { get; set; } = 1.0f;

        [Signal] public delegate void DashStartedEventHandler(Vector2 direction);
        [Signal] public delegate void DashEndedEventHandler();

        private CharacterBody2D? _body;
        private float _dashTimer;
        private float _cooldownTimer;
        private Vector2 _dashDirection;
        private Vector2 _gravityBackup;

        public bool IsDashing => _dashTimer > 0;
        public bool IsOnCooldown => _cooldownTimer > 0;
        public bool IsInvincible => GrantIFrames && _dashTimer > 0;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            if (_dashTimer > 0)
            {
                _dashTimer -= dt;
                _body.Velocity = _dashDirection * DashSpeed;
                _body.MoveAndSlide();
                if (_dashTimer <= 0) EmitSignal(SignalName.DashEnded);
            }
            else
            {
                _cooldownTimer = Mathf.Max(0, _cooldownTimer - dt);

                // Check for dash input.
                if (Input.IsActionJustPressed(DashAction) && _cooldownTimer <= 0)
                {
                    // Determine direction from input, fall back to facing.
                    float x = Input.GetAxis("move_left", "move_right");
                    float y = Input.GetAxis("move_up", "move_down");
                    _dashDirection = new Vector2(x, y);
                    if (_dashDirection == Vector2.Zero)
                        _dashDirection = new Vector2(_body.Velocity.X >= 0 ? 1f : -1f, 0f);
                    _dashDirection = _dashDirection.Normalized();

                    _dashTimer = DashDuration;
                    _cooldownTimer = DashCooldown;
                    EmitSignal(SignalName.DashStarted, _dashDirection);
                }
            }
        }

        /// <summary>Reset cooldown (e.g. on landing for "ground dash only" games).</summary>
        public void ResetCooldown() => _cooldownTimer = 0;
    }
}
