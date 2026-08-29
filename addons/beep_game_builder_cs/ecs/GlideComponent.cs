using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Glide/parachute ability component. Attach to a CharacterBody2D. While
    /// falling and the glide input is held, the body descends slowly with
    /// horizontal air control — like a wingsuit, cape, or parachute. Good for
    /// long jumps, exploring large levels, or soft landings.
    ///
    /// Composable — stack alongside Jump, Dash, Slide, Hover, WallJump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GlideComponent : ControllerComponent
    {
        [ExportGroup("Glide")]
        [Export] public float GlideFallSpeed { get; set; } = 40f;
        [Export] public float GlideAirSpeed { get; set; } = 250f;
        [Export] public float GlideAccel { get; set; } = 600f;
        [Export] public string GlideAction { get; set; } = "jump";

        [Signal] public delegate void GlideStartedEventHandler();
        [Signal] public delegate void GlideEndedEventHandler();

        public float EffectiveGlideFallSpeed => NonNegative(GlideFallSpeed);
        public float EffectiveGlideAirSpeed => NonNegative(GlideAirSpeed);
        public float EffectiveGlideAccel => NonNegative(GlideAccel);

        private CharacterBody2D? _body;
        private bool _isGliding;

        public bool IsGliding => _isGliding;

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
                if (_isGliding)
                {
                    _isGliding = false;
                    EmitSignal(SignalName.GlideEnded);
                }
                return;
            }
            // Gate input reads so absent actions don't spam per-frame errors pre-generation.
            if (!InputActionsAvailable(GlideAction, "move_left", "move_right")) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            bool onFloor = _body.IsOnFloor();
            bool falling = _body.Velocity.Y > 0;
            bool inputHeld = Input.IsActionPressed(GlideAction);

            // Can glide: in air (not on floor), falling, input held.
            bool canGlide = !onFloor && falling && inputHeld;

            if (canGlide)
            {
                if (!_isGliding)
                {
                    _isGliding = true;
                    EmitSignal(SignalName.GlideStarted);
                }

                // Horizontal air control during glide.
                float inputX = Input.GetAxis("move_left", "move_right");
                float targetX = inputX * EffectiveGlideAirSpeed;
                float newX = Mathf.MoveToward(_body.Velocity.X, targetX, EffectiveGlideAccel * dt);

                // CAP the descent at GlideFallSpeed. +Y is down, so this must be Min: Max let any
                // faster fall through, so gliding never actually slowed the fall.
                _body.Velocity = new Vector2(newX, Mathf.Min(EffectiveGlideFallSpeed, _body.Velocity.Y));
            }
            else if (_isGliding)
            {
                _isGliding = false;
                EmitSignal(SignalName.GlideEnded);
            }
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
