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

        private CharacterBody2D? _body;
        private bool _isGliding;

        public bool IsGliding => _isGliding;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            bool onFloor = _body.IsOnFloor();
            bool falling = _body.Velocity.Y > 0;
            bool inputHeld = Input.IsActionPressed(GlideAction);

            // Can glide: in air, falling, input held, not on floor.
            bool canGlide = !onFloor && falling && inputHeld;

            if (canGlide)
            {
                if (!_isGliding)
                {
                    _isGliding = true;
                    EmitSignal(SignalName.GlideStarted);
                }
                // Override fall speed with slow glide descent.
                _body.Velocity = new Vector2(_body.Velocity.X, GlideFallSpeed);

                // Horizontal air control during glide.
                float inputX = Input.GetAxis("move_left", "move_right");
                float targetX = inputX * GlideAirSpeed;
                _body.Velocity = new Vector2(
                    Mathf.MoveToward(_body.Velocity.X, targetX, GlideAccel * dt),
                    _body.Velocity.Y);
            }
            else if (_isGliding)
            {
                _isGliding = false;
                EmitSignal(SignalName.GlideEnded);
            }
        }
    }
}
