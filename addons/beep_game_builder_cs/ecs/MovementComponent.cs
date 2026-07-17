using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Movement component for any entity that moves.
    /// Blind — works for player, NPC, projectile, or camera follow target.
    ///
    /// Attach to a child of a CharacterBody2D. Each physics frame it accelerates toward
    /// <see cref="DesiredDirection"/> (or the move_* input actions when
    /// <see cref="ReadInput"/> is on), then drives the body.
    ///
    /// It used to only *compute* Velocity and leave applying it to a caller — and there was
    /// no caller: Move() had zero callers addon-wide and nothing ever read Velocity, so the
    /// three templates carrying this component (player, enemy, robot NPC) could not move at
    /// all. A duplicated player_template was a static prop.
    ///
    /// Use this OR a genre ControllerComponent (PlatformerController, TopDownController…),
    /// never both on one body — they would both call MoveAndSlide and fight. The controllers
    /// are self-contained and do not use this component; this is the genre-neutral option,
    /// and the one an AI can steer by setting DesiredDirection.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MovementComponent : GameplayComponent
    {
        [Export] public float Speed { get; set; } = 200f;
        [Export] public float Acceleration { get; set; } = 1800f;
        [Export] public float Friction { get; set; } = 1400f;

        /// <summary>Drive from the move_left/right/up/down actions (8-way). Turn on for a
        /// player; leave off for an NPC whose AI sets <see cref="DesiredDirection"/>.</summary>
        [Export] public bool ReadInput { get; set; } = false;

        // Dash lived here AND in the dedicated DashComponent (which also has i-frames +
        // afterimage + input handling). Removed from here — use DashComponent for dashing.

        [Signal] public delegate void MovedEventHandler(Vector2 direction, float speed);
        [Signal] public delegate void StoppedEventHandler();

        public Vector2 Velocity { get; set; }

        /// <summary>Where to steer. Set by an AI/controller, or by input when
        /// <see cref="ReadInput"/> is on. Zero = decelerate to a stop.</summary>
        public Vector2 DesiredDirection { get; set; }

        private CharacterBody2D? _body;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _body = GetParent() as CharacterBody2D;
            if (_body == null)
            {
                GD.PushWarning($"[{Name}] MovementComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a CharacterBody2D — it will compute a velocity but nothing will move. Attach it under the body you want moved.");
                return;
            }

            // Both would call MoveAndSlide on the same body in the same frame.
            if (GetSiblingComponent<ControllerComponent>() is { } controller)
                GD.PushWarning($"[{Name}] MovementComponent shares a body with {controller.GetType().Name}, which does its own movement — they will fight. Use one or the other.");
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_body == null || !IsActive) return;

            if (ReadInput)
                DesiredDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");

            Move(DesiredDirection, delta);

            // Actually drive the body. Computing Velocity and stopping there is what made
            // this component inert in every scene that shipped it.
            _body.Velocity = Velocity;
            _body.MoveAndSlide();
            // Take back what the collision solver actually allowed, so running into a wall
            // doesn't keep accumulating speed into it.
            Velocity = _body.Velocity;
        }

        public void Move(Vector2 direction, double delta)
        {
            if (!IsActive) return;
            DesiredDirection = direction;

            if (direction.Length() > 0)
            {
                Velocity = Velocity.MoveToward(direction * Speed, Acceleration * (float)delta);
                EmitSignal(SignalName.Moved, direction, Speed);
            }
            else
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
                if (Velocity.Length() < 1f) EmitSignal(SignalName.Stopped);
            }
        }
    }
}
