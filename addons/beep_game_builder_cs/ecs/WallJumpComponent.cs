using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Wall slide + wall jump ability component. Attach to a CharacterBody2D.
    /// When the body touches a wall while in the air, it slides down slowly.
    /// Pressing jump while wall-sliding launches the body away from the wall
    /// (wall jump). Direction of jump is away from the wall.
    ///
    /// Composable — stack alongside Jump, Dash, Slide, Glide, Hover.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WallJumpComponent : ControllerComponent
    {
        [ExportGroup("Wall Detection")]
        [Export] public float RayDistance { get; set; } = 12f;
        [Export] public uint CollisionMask { get; set; } = 0xFFFFFFFF;

        [ExportGroup("Wall Slide")]
        [Export] public float WallSlideSpeed { get; set; } = 60f;
        [Export] public float WallStickTime { get; set; } = 0.25f;

        [ExportGroup("Wall Jump")]
        [Export] public float WallJumpForceX { get; set; } = 350f;
        [Export] public float WallJumpForceY { get; set; } = -400f;
        [Export] public float WallJumpLockTime { get; set; } = 0.15f;

        [Signal] public delegate void WallSlideStartedEventHandler(int wallDirection);
        [Signal] public delegate void WallJumpedEventHandler(int wallDirection);

        public float EffectiveRayDistance => NonNegative(RayDistance);
        public float EffectiveWallSlideSpeed => NonNegative(WallSlideSpeed);
        public float EffectiveWallStickTime => NonNegative(WallStickTime);
        public float EffectiveWallJumpForceX => NonNegative(WallJumpForceX);
        public float EffectiveWallJumpForceY => float.IsFinite(WallJumpForceY) ? -Mathf.Abs(WallJumpForceY) : -400f;
        public float EffectiveWallJumpLockTime => NonNegative(WallJumpLockTime);

        private CharacterBody2D? _body;
        private RayCast2D? _leftRay;
        private RayCast2D? _rightRay;
        // Only the rays THIS component injected are freed on exit — a pre-existing WallRayLeft/Right
        // authored in the scene is left alone.
        private bool _createdLeftRay;
        private bool _createdRightRay;
        private bool _isWallSliding;
        private float _stickTimer;
        private float _lockTimer;
        private int _wallDirection;
        private int _kickDirection;
        private int _slideDirection;
        private ulong _jumpFrame;
        private bool _hasJumpFrame;
        private bool _launchPending;

        public bool IsWallSliding => IsActive && _isWallSliding;
        public bool JumpedThisFrame => IsActive && (_launchPending || (_hasJumpFrame && _jumpFrame == Engine.GetPhysicsFrames()));
        public bool IsWallJumpLocked => IsActive && _lockTimer > 0;

        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            if (_body == null)
            {
                GD.PushWarning($"[{Name}] WallJumpComponent could not resolve a CharacterBody2D parent — wall detection will not run. Attach it to a CharacterBody2D or provide a valid body path.");
                return;
            }
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            ProcessPhysicsPriority = -8;
            SetupWallRays();
        }

        private void SetupWallRays()
        {
            if (Engine.IsEditorHint()) return;
            if (_body == null) return;
            // Create or find wall-detection rays on the body.
            _leftRay = _body.GetNodeOrNull<RayCast2D>("WallRayLeft");
            _rightRay = _body.GetNodeOrNull<RayCast2D>("WallRayRight");
            // A same-frame reattachment must not adopt rays already scheduled for deletion.
            if (_leftRay?.IsQueuedForDeletion() == true) { _leftRay.Name = $"RetiredWallRay{_leftRay.GetInstanceId()}"; _leftRay = null; }
            if (_rightRay?.IsQueuedForDeletion() == true) { _rightRay.Name = $"RetiredWallRay{_rightRay.GetInstanceId()}"; _rightRay = null; }
            if (_leftRay == null)
            {
                _leftRay = new RayCast2D
                {
                    Name = "WallRayLeft",
                    TargetPosition = new Vector2(-EffectiveRayDistance, 0),
                    CollisionMask = CollisionMask
                };
                _body.AddChild(_leftRay);
                _leftRay.Enabled = true;
                _createdLeftRay = true;
            }
            if (_rightRay == null)
            {
                _rightRay = new RayCast2D
                {
                    Name = "WallRayRight",
                    TargetPosition = new Vector2(EffectiveRayDistance, 0),
                    CollisionMask = CollisionMask
                };
                _body.AddChild(_rightRay);
                _rightRay.Enabled = true;
                _createdRightRay = true;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // Free the rays we injected into the body so detaching the component doesn't orphan
            // them (and re-adding a WallJumpComponent doesn't stack duplicates).
            if (_createdLeftRay && _leftRay != null && GodotObject.IsInstanceValid(_leftRay))
                _leftRay.QueueFree();
            if (_createdRightRay && _rightRay != null && GodotObject.IsInstanceValid(_rightRay))
                _rightRay.QueueFree();
            _leftRay = _rightRay = null;
            _createdLeftRay = _createdRightRay = false;
            _body = null; _statusEffects = null;
            ResetWallMotion();
            RequestReady();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body)) return;
            var actor = ActorComponent.ForBody(_body);
            if (!IsActive || actor is { IsActive: false } or { IsDead: true } or { HasOrders: true }
                || GetSiblingComponent<GridPathFollowerComponent>() is { IsMoving: true }
                || GetSiblingComponent<DashComponent>() is { IsDashing: true }
                || CharacterMotion.HasKnockback(_body)
                || _statusEffects?.HasEffect("stun") == true)
            {
                ResetWallMotion();
                return;
            }
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            _lockTimer = Mathf.Max(0, _lockTimer - dt);

            // Detect wall direction.
            _wallDirection = 0;
            if (GodotObject.IsInstanceValid(_leftRay)) _leftRay!.ForceRaycastUpdate();
            if (GodotObject.IsInstanceValid(_rightRay)) _rightRay!.ForceRaycastUpdate();
            if (GodotObject.IsInstanceValid(_rightRay) && _rightRay!.IsColliding()) _wallDirection = 1;
            else if (GodotObject.IsInstanceValid(_leftRay) && _leftRay!.IsColliding()) _wallDirection = -1;

            bool onFloor = CharacterMotion.IsOnFloor(_body);
            bool falling = _body.Velocity.Y > 0;

            // Wall slide: touching a wall, in the air, falling.
            if (_wallDirection != 0 && !onFloor && falling && _lockTimer <= 0)
            {
                _slideDirection = _wallDirection;
                if (!_isWallSliding)
                {
                    _isWallSliding = true;
                    EmitSignal(SignalName.WallSlideStarted, _wallDirection);
                }
                // Clamp fall speed.
                _body.Velocity = new Vector2(_body.Velocity.X, Mathf.Min(_body.Velocity.Y, EffectiveWallSlideSpeed));
                _stickTimer = EffectiveWallStickTime;
            }
            else if (_isWallSliding)
            {
                _stickTimer -= dt;
                if (_stickTimer <= 0 || onFloor)
                    _isWallSliding = false;
            }

            // Wall jump. Gate the input read so an absent "jump" action doesn't spam a
            // per-frame error before the input map is generated.
            if (_isWallSliding && (actor?.ConsumeJump() ?? (InputActionsAvailable("jump") && Input.IsActionJustPressed("jump"))))
            {
                _body.Velocity = new Vector2(-_slideDirection * EffectiveWallJumpForceX, EffectiveWallJumpForceY);
                _kickDirection = _slideDirection;
                _jumpFrame = Engine.GetPhysicsFrames(); _hasJumpFrame = true;
                _launchPending = true;
                _isWallSliding = false;
                _lockTimer = EffectiveWallJumpLockTime; // prevent immediate re-stick
                EmitSignal(SignalName.WallJumped, _slideDirection);
            }
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private void ResetWallMotion()
        {
            _isWallSliding = _hasJumpFrame = _launchPending = false;
            _stickTimer = _lockTimer = 0;
            _wallDirection = _kickDirection = _slideDirection = 0;
        }

        public void CancelWallMotion() => ResetWallMotion();
        internal void CompleteIntegration() => _launchPending = false;

        internal Vector2 ApplyVelocity(Vector2 ordinary)
        {
            if (!IsActive) return ordinary;
            if (IsWallJumpLocked || JumpedThisFrame) ordinary.X = -_kickDirection * EffectiveWallJumpForceX;
            if (JumpedThisFrame) ordinary.Y = EffectiveWallJumpForceY;
            else if (_isWallSliding) ordinary.Y = Mathf.Min(ordinary.Y, EffectiveWallSlideSpeed);
            return ordinary;
        }

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
