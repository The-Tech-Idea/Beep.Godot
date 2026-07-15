using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Top-down movement controller component. Attach to any CharacterBody2D.
    /// Blind — works for players, NPCs, enemies with top-down movement.
    /// Uses Input actions: move_left, move_right, move_up, move_down.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TopDownController : ControllerComponent
    {
        [Export] public float Speed { get; set; } = 220f;
        [Export] public float Acceleration { get; set; } = 1800f;
        [Export] public float Friction { get; set; } = 1400f;
        [Export] public bool StunBlocksMovement { get; set; } = true;

        [Signal] public delegate void MovedEventHandler(Vector2 direction);
        [Signal] public delegate void StoppedEventHandler();

        private CharacterBody2D? _body;
        private StatusEffectComponent? _statusEffects;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            var info = GameBuilder.GameInfo.Instance;
            if (info != null) Speed = info.MoveSpeed;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;

            bool isStunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");
            var input = isStunned ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");

            float speedMod = _statusEffects?.GetModifier("speed_boost", "speed_multiplier", 1f) ?? 1f;
            float finalSpeed = Speed * speedMod;

            if (input.Length() > 0)
            {
                _body.Velocity = _body.Velocity.MoveToward(input * finalSpeed, Acceleration * (float)delta);
                EmitSignal(SignalName.Moved, input);
            }
            else
            {
                _body.Velocity = _body.Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
                if (_body.Velocity.Length() < 1f) EmitSignal(SignalName.Stopped);
            }
            _body.MoveAndSlide();
        }
    }
}
