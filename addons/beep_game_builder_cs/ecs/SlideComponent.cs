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
        private float _slideSpeed;
        private bool _inputHeld;
        private bool _sliding;
        private RectangleShape2D? _standingShape;
        private RectangleShape2D? _slideShape;
        private Transform2D _standingTransform;

        public bool IsSliding => IsActive && _sliding;
        public bool IsCollisionReduced => _slideShape is not null;

        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            ProcessPhysicsPriority = -4;
            // Find the collision shape to shrink during slide.
            if (_body != null)
            {
                foreach (var child in _body.GetChildren())
                {
                    if (child is CollisionShape2D cs)
                    {
                        _collision = cs;
                        if (ShrinkCollision && cs.Shape is not RectangleShape2D)
                            GD.PushWarning($"[Slide] Collision shape must be RectangleShape2D for ShrinkCollision, got {cs.Shape?.GetType().Name}");
                        break;
                    }
                }
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || !GodotObject.IsInstanceValid(_body)) return;
            var actor = ActorComponent.ForBody(_body);
            bool held = actor?.IsAbilityHeld(SlideAction)
                ?? (InputActionsAvailable(SlideAction) && Input.IsActionPressed(SlideAction));
            bool pressed = held && !_inputHeld;
            _inputHeld = held;
            if (IsInterrupted(actor))
            {
                CancelSlide();
                TryRestoreCollision();
                return;
            }
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            if (_slideTimer > 0)
            {
                _slideTimer -= dt;
                _slideSpeed = Mathf.MoveToward(_slideSpeed, 0, EffectiveSlideDeceleration * dt);
                if (_slideTimer <= 0 || !CharacterMotion.IsOnFloor(_body) || _slideSpeed <= 0)
                    CancelSlide();
            }
            else
            {
                if (pressed) TrySlide();
            }
            if (_slideTimer <= 0) TryRestoreCollision();
        }

        public override void _ExitTree()
        {
            CancelSlide();
            RestoreCollision();
            _body = null; _collision = null; _statusEffects = null;
            _inputHeld = false; _slideSpeed = _slideDirection = 0;
            RequestReady();
            base._ExitTree();
        }

        private bool IsInterrupted(ActorComponent? actor) => !IsActive
            || actor is { IsActive: false } or { IsDead: true } or { HasOrders: true }
            || GetSiblingComponent<GridPathFollowerComponent>() is { IsMoving: true }
            || GetSiblingComponent<DashComponent>() is { IsDashing: true }
            || CharacterMotion.HasKnockback(_body!) || _statusEffects?.HasEffect("stun") == true;

        public bool TrySlide()
        {
            if (!GodotObject.IsInstanceValid(_body) || IsSliding || IsCollisionReduced
                || IsInterrupted(ActorComponent.ForBody(_body)) || !CharacterMotion.IsOnFloor(_body!)
                || !IsFinite(_body.Velocity) || Mathf.Abs(_body.Velocity.X) <= 50f
                || EffectiveSlideDuration <= 0f || EffectiveSlideSpeed <= 0f) return false;

            _slideTimer = EffectiveSlideDuration;
            _sliding = true;
            _slideDirection = _body!.Velocity.X >= 0 ? 1f : -1f;
            _slideSpeed = Mathf.Min(Mathf.Abs(_body.Velocity.X), EffectiveSlideSpeed);

            if (ShrinkCollision && GodotObject.IsInstanceValid(_collision)
                && !_collision!.Disabled && _collision.Shape is RectangleShape2D)
                ReduceCollision(EffectiveHeightMultiplier);

            EmitSignal(SignalName.SlideStarted);
            return true;
        }

        private void ReduceCollision(float ratio)
        {
            if (_collision?.Shape is RectangleShape2D rect)
            {
                _standingShape = rect;
                _standingTransform = _collision.Transform;
                _slideShape = (RectangleShape2D)rect.Duplicate();
                _slideShape.Size = new(rect.Size.X, rect.Size.Y * ratio);
                _collision.Shape = _slideShape;
                // Offset in the authored shape's local basis so the feet do not move.
                _collision.Position = _standingTransform * new Vector2(0, (rect.Size.Y - _slideShape.Size.Y) * 0.5f);
            }

        }

        public void CancelSlide()
        {
            bool wasSliding = _sliding;
            _sliding = false;
            _slideTimer = 0;
            if (wasSliding) EmitSignal(SignalName.SlideEnded);
        }

        internal Vector2 ApplyVelocity(Vector2 ordinary) => IsSliding ? new(_slideDirection * _slideSpeed, ordinary.Y) : ordinary;

        private void TryRestoreCollision()
        {
            if (_slideShape is null) return;
            if (!GodotObject.IsInstanceValid(_collision) || _collision!.Shape != _slideShape || _collision.Disabled)
            { RestoreCollision(); return; }
            var excluded = new Godot.Collections.Array<Rid> { _body!.GetRid() };
            foreach (var exception in _body.GetCollisionExceptions())
                if (GodotObject.IsInstanceValid(exception)) excluded.Add(exception.GetRid());
            using var query = new PhysicsShapeQueryParameters2D
            {
                Shape = _standingShape,
                Transform = _body!.GlobalTransform * _standingTransform,
                CollisionMask = _body.CollisionMask,
                Exclude = excluded
            };
            if (_body.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0)
                RestoreCollision();
        }

        private void RestoreCollision()
        {
            if (_slideShape is null) return;
            if (GodotObject.IsInstanceValid(_collision) && _collision!.Shape == _slideShape)
            {
                _collision.Shape = _standingShape;
                _collision.Transform = _standingTransform;
            }
            _slideShape.Dispose(); _slideShape = null; _standingShape = null;
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
