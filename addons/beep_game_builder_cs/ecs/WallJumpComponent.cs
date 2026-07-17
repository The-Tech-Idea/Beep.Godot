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

        private CharacterBody2D? _body;
        private RayCast2D? _leftRay;
        private RayCast2D? _rightRay;
        private bool _isWallSliding;
        private float _stickTimer;
        private float _lockTimer;
        private int _wallDirection;

        public bool IsWallSliding => _isWallSliding;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            SetupWallRays();
        }

        private void SetupWallRays()
        {
            if (Engine.IsEditorHint()) return;
            if (_body == null) return;
            // Create or find wall-detection rays on the body.
            _leftRay = _body.GetNodeOrNull<RayCast2D>("WallRayLeft");
            _rightRay = _body.GetNodeOrNull<RayCast2D>("WallRayRight");
            if (_leftRay == null)
            {
                _leftRay = new RayCast2D
                {
                    Name = "WallRayLeft",
                    TargetPosition = new Vector2(-RayDistance, 0),
                    CollisionMask = CollisionMask
                };
                _body.AddChild(_leftRay);
                _leftRay.Enabled = true;
            }
            if (_rightRay == null)
            {
                _rightRay = new RayCast2D
                {
                    Name = "WallRayRight",
                    TargetPosition = new Vector2(RayDistance, 0),
                    CollisionMask = CollisionMask
                };
                _body.AddChild(_rightRay);
                _rightRay.Enabled = true;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            _lockTimer = Mathf.Max(0, _lockTimer - dt);

            // Detect wall direction.
            _wallDirection = 0;
            if (_rightRay?.IsColliding() == true) _wallDirection = 1;
            else if (_leftRay?.IsColliding() == true) _wallDirection = -1;

            bool onFloor = _body.IsOnFloor();
            bool falling = _body.Velocity.Y > 0;

            // Wall slide: touching a wall, in the air, falling.
            if (_wallDirection != 0 && !onFloor && falling && _lockTimer <= 0)
            {
                if (!_isWallSliding)
                {
                    _isWallSliding = true;
                    EmitSignal(SignalName.WallSlideStarted, _wallDirection);
                }
                // Clamp fall speed.
                _body.Velocity = new Vector2(_body.Velocity.X, Mathf.Min(_body.Velocity.Y, WallSlideSpeed));
                _stickTimer = WallStickTime;
            }
            else if (_isWallSliding)
            {
                _stickTimer -= dt;
                if (_stickTimer <= 0 || onFloor)
                    _isWallSliding = false;
            }

            // Wall jump.
            if (_isWallSliding && Input.IsActionJustPressed("jump"))
            {
                _body.Velocity = new Vector2(-_wallDirection * WallJumpForceX, WallJumpForceY);
                _isWallSliding = false;
                _lockTimer = WallJumpLockTime; // prevent immediate re-stick
                EmitSignal(SignalName.WallJumped, _wallDirection);
            }
        }
    }
}
