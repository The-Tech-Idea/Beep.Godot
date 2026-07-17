using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Top-down shooter controller for a CharacterBody2D. 8-directional movement
    /// + look_at the mouse + fire from a muzzle Marker2D at a configurable rate.
    /// Emits FireFired(muzzleGlobalPos, direction) so a projectile spawner can
    /// react. Reads tuning (MoveSpeed, FireRate) from GameInfo if present.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ShooterController : ControllerComponent
    {
        [Export] public float MoveSpeed { get; set; } = 250f;
        [Export] public float FireRate { get; set; } = 0.2f;
        [Export] public float ProjectileDamage { get; set; } = 10f;
        [Export] public float ProjectileSpeed { get; set; } = 500f;
        [Export] public string FireAction { get; set; } = "attack";
        /// <summary>Where shots originate. Relative to THIS node. Default "../Muzzle": the
        /// house layout puts the controller and the muzzle marker side by side under the
        /// player (see shooter_main.tscn), so the old "Muzzle" default resolved to
        /// Player/Controller/Muzzle — which never exists. GetNodeOrNull made that silent, and
        /// shots fell back to the body's center, quietly ignoring the marker's offset.</summary>
        [Export] public NodePath MuzzlePath { get; set; } = new("../Muzzle");
        [Export] public PackedScene? ProjectileScene { get; set; }
        [Export] public bool StunBlocksMovement { get; set; } = true;

        [Signal] public delegate void FireFiredEventHandler(Vector2 position, Vector2 direction);

        private CharacterBody2D? _body;
        private Marker2D? _muzzle;
        private StatusEffectComponent? _statusEffects;
        private double _cooldown;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            // Fall back to a Marker2D named "Muzzle" on the body, so either layout works
            // (marker beside the controller, or under it) without editing every scene.
            _muzzle = GetNodeOrNull<Marker2D>(MuzzlePath) ?? _body?.GetNodeOrNull<Marker2D>("Muzzle");
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            var info = GameBuilder.GameInfo.Instance;
            if (info != null) { MoveSpeed = info.MoveSpeed; FireRate = info.FireRate; }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _body == null || Engine.IsEditorHint()) return;

            bool isStunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");
            Vector2 input = isStunned ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");

            float speedMod = _statusEffects?.GetModifier("speed_boost", "speed_multiplier", 1f) ?? 1f;
            _body.Velocity = input * MoveSpeed * speedMod;
            _body.MoveAndSlide();

            // Aim toward mouse.
            Vector2 mouse = _body.GetGlobalMousePosition();
            _body.Rotation = (mouse - _body.GlobalPosition).Angle();

            // Fire.
            _cooldown -= delta;
            if (Input.IsActionPressed(FireAction) && _cooldown < 0)
            {
                GetViewport().SetInputAsHandled();
                _cooldown = 1.0 / FireRate;
                Vector2 muzzlePos = _muzzle?.GlobalPosition ?? _body.GlobalPosition;
                Vector2 dir = Vector2.FromAngle(_body.Rotation);
                EmitSignal(SignalName.FireFired, muzzlePos, dir);
                SpawnProjectile(muzzlePos, dir);
            }
        }

        private void SpawnProjectile(Vector2 pos, Vector2 dir)
        {
            if (ProjectileScene == null) return;
            var root = GetTree().CurrentScene;
            var pool = root.GetNodeOrNull<Node>("Projectiles") ?? root;
            var proj = ProjectileScene.Instantiate();
            pool.AddChild(proj);
            if (proj is Node2D n2d)
            {
                n2d.GlobalPosition = pos;
                n2d.Rotation = dir.Angle();

                var projComp = EntityComponent.FindComponent<ProjectileComponent>(n2d, false);
                if (projComp != null)
                {
                    projComp.Damage = ProjectileDamage;
                    projComp.Speed = ProjectileSpeed;
                    projComp.Launch(dir);
                }
            }
        }
    }
}
