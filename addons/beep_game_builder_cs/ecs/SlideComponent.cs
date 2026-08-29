using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Ground slide ability component. Attach to a CharacterBody2D. When activated
    /// (input "slide" or "crouch" + move direction), the body slides at high speed
    /// with reduced collision height, maintaining momentum. Good for dodging under
    /// obstacles, crossing gaps, or speedrunning.
    ///
    /// Composable — stack alongside Jump, Dash, Glide, Hover, WallJump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SlideComponent : ControllerComponent
    {
        [ExportGroup("Slide")]
        [Export] public float SlideSpeed { get; set; } = 500f;
        [Export] public float SlideDuration { get; set; } = 0.6f;
        [Export] public float SlideDeceleration { get; set; } = 600f;
        [Export] public string SlideAction { get; set; } = "crouch";

        [ExportGroup("Size Change")]
        [Export] public bool ShrinkCollision { get; set; } = true;
        [Export] public float HeightMultiplier { get; set; } = 0.5f;

        [Signal] public delegate void SlideStartedEventHandler();
        [Signal] public delegate void SlideEndedEventHandler();

        public float EffectiveSlideSpeed => NonNegative(SlideSpeed);
        public float EffectiveSlideDuration => NonNegative(SlideDuration);
        public float EffectiveSlideDeceleration => NonNegative(SlideDeceleration);
        public float EffectiveHeightMultiplier => float.IsFinite(HeightMultiplier) ? Mathf.Clamp(HeightMultiplier, 0.1f, 1f) : 1f;

        private CharacterBody2D? _body;
        private CollisionShape2D? _collision;
        private float _slideTimer;
        private float _slideDirection;
        private Vector2 _originalShapeSize;

        public bool IsSliding => _slideTimer > 0;

        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            // Find the collision shape to shrink during slide.
            if (_body != null && ShrinkCollision)
            {
                foreach (var child in _body.GetChildren())
                {
                    if (child is CollisionShape2D cs)
                    {
                        _collision = cs;
                        if (cs.Shape is RectangleShape2D rect)
                            _originalShapeSize = rect.Size;
                        else
                            GD.PushWarning($"[Slide] Collision shape must be RectangleShape2D for ShrinkCollision, got {cs.Shape?.GetType().Name}");
                        break;
                    }
                }
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body) || !IsActive) return;
            if (_statusEffects != null && _statusEffects.HasEffect("stun"))
            {
                if (_slideTimer > 0)
                    EndSlide();
                return;
            }
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            if (_slideTimer > 0)
            {
                _slideTimer -= dt;
                // Decelerate during slide.
                float currentSpeed = Mathf.MoveToward(Mathf.Abs(_body.Velocity.X), 0, EffectiveSlideDeceleration * dt);
                _body.Velocity = new Vector2(_slideDirection * currentSpeed, _body.Velocity.Y);
                // Only SET velocity — the sibling controller owns MoveAndSlide. Calling it here too
                // integrated the body twice per frame (~2× slide distance), like Jump/Glide/WallJump
                // which correctly only set Velocity.

                if (_slideTimer <= 0 || !_body.IsOnFloor())
                    EndSlide();
            }
            else
            {
                // Start slide: crouch + moving. Gate the input read so an absent action doesn't
                // spam a per-frame error before the input map is generated.
                if (InputActionsAvailable(SlideAction)
                    && Input.IsActionPressed(SlideAction) && _body.IsOnFloor() && Mathf.Abs(_body.Velocity.X) > 50f)
                {
                    StartSlide();
                }
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // StartSlide shrinks the CollisionShape2D's shape, which is a SHARED resource when
            // several bodies reference the same .tres. Freed mid-slide, EndSlide never runs and
            // the shape stays shrunk for every other instance. Restore it here.
            if (ShrinkCollision && _slideTimer > 0 && _collision?.Shape is RectangleShape2D rect)
                rect.Size = _originalShapeSize;
        }

        private void StartSlide()
        {
            if (EffectiveSlideDuration <= 0f || EffectiveSlideSpeed <= 0f)
                return;

            _slideTimer = EffectiveSlideDuration;
            _slideDirection = _body!.Velocity.X >= 0 ? 1f : -1f;
            float slideVelocity = Mathf.Min(Mathf.Abs(_body.Velocity.X), EffectiveSlideSpeed);
            _body.Velocity = new Vector2(_slideDirection * slideVelocity, _body.Velocity.Y);

            if (ShrinkCollision && _collision?.Shape is RectangleShape2D rect)
                rect.Size = new Vector2(_originalShapeSize.X, _originalShapeSize.Y * EffectiveHeightMultiplier);

            EmitSignal(SignalName.SlideStarted);
        }

        private void EndSlide()
        {
            _slideTimer = 0;
            if (ShrinkCollision && _collision?.Shape is RectangleShape2D rect)
                rect.Size = _originalShapeSize;
            EmitSignal(SignalName.SlideEnded);
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
