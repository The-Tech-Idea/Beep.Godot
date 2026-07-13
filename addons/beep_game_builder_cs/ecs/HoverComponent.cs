using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Hover ability component. Attach to a CharacterBody2D. While the hover input
    /// ("jump" held in air, or a dedicated "hover" action) is held, the body floats
    /// with near-zero gravity. Good for precision platforming, aerial combat, or
    /// jetpack-style mechanics.
    ///
    /// Composable — stack alongside Jump, Dash, Slide, Glide, WallJump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HoverComponent : ControllerComponent
    {
        [ExportGroup("Hover")]
        [Export] public float HoverGravity { get; set; } = 30f;
        [Export] public float MaxHoverTime { get; set; } = 2f;
        [Export] public float HoverCooldown { get; set; } = 0.5f;
        [Export] public string HoverAction { get; set; } = "jump";

        [Signal] public delegate void HoverStartedEventHandler();
        [Signal] public delegate void HoverEndedEventHandler();

        private CharacterBody2D? _body;
        private float _hoverTimer;
        private float _cooldownTimer;
        private bool _isHovering;

        public bool IsHovering => _isHovering;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            _cooldownTimer = Mathf.Max(0, _cooldownTimer - dt);

            bool onFloor = _body.IsOnFloor();
            bool inputHeld = Input.IsActionPressed(HoverAction);

            // Can hover: in air, input held, time remaining, not on cooldown.
            if (inputHeld && !onFloor && _hoverTimer < MaxHoverTime && _cooldownTimer <= 0)
            {
                if (!_isHovering)
                {
                    _isHovering = true;
                    EmitSignal(SignalName.HoverStarted);
                }
                // Override gravity with hover gravity.
                _body.Velocity = new Vector2(_body.Velocity.X, HoverGravity);
                _hoverTimer += dt;
            }
            else if (_isHovering)
            {
                _isHovering = false;
                _cooldownTimer = HoverCooldown;
                EmitSignal(SignalName.HoverEnded);
            }

            // Reset hover time when landing.
            if (onFloor) _hoverTimer = 0;
        }
    }
}
