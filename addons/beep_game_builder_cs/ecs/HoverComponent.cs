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

        public float EffectiveHoverGravity => NonNegative(HoverGravity);
        public float EffectiveMaxHoverTime => NonNegative(MaxHoverTime);
        public float EffectiveHoverCooldown => NonNegative(HoverCooldown);

        private CharacterBody2D? _body;
        private float _hoverTimer;
        private float _cooldownTimer;
        private bool _isHovering;

        public bool IsHovering => _isHovering;

        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body) || !IsActive) return;
            if (_statusEffects != null && _statusEffects.HasEffect("stun"))
            {
                if (_isHovering)
                {
                    _isHovering = false;
                    _cooldownTimer = EffectiveHoverCooldown;
                    EmitSignal(SignalName.HoverEnded);
                }
                return;
            }
            // Gate input reads so an absent action doesn't spam per-frame errors pre-generation.
            if (!InputActionsAvailable(HoverAction)) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            _cooldownTimer = Mathf.Max(0, _cooldownTimer - dt);

            bool onFloor = _body.IsOnFloor();
            bool inputHeld = Input.IsActionPressed(HoverAction);

            // Can hover: in air, input held, time remaining, not on cooldown.
            if (inputHeld && !onFloor && _hoverTimer < EffectiveMaxHoverTime && _cooldownTimer <= 0)
            {
                if (!_isHovering)
                {
                    _isHovering = true;
                    EmitSignal(SignalName.HoverStarted);
                }
                // CAP the descent at HoverGravity (+Y is down): Max let a fast fall through, so
                // hover never floated. Min holds the fall speed to the gentle hover value.
                _body.Velocity = new Vector2(_body.Velocity.X, Mathf.Min(EffectiveHoverGravity, _body.Velocity.Y));
                _hoverTimer += dt;
            }
            else if (_isHovering)
            {
                _isHovering = false;
                _cooldownTimer = EffectiveHoverCooldown;
                EmitSignal(SignalName.HoverEnded);
            }

            // Reset hover time when landing.
            if (onFloor)
                _hoverTimer = 0;
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
