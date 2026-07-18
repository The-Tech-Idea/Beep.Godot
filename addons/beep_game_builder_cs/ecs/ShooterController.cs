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
        private StatsComponent? _stats;
        private EquipmentComponent? _equipment;
        private double _cooldown;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            // Fall back to a Marker2D named "Muzzle" on the body, so either layout works
            // (marker beside the controller, or under it) without editing every scene.
            _muzzle = GetNodeOrNull<Marker2D>(MuzzlePath) ?? _body?.GetNodeOrNull<Marker2D>("Muzzle");
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _stats = GetSiblingComponent<StatsComponent>();
            _equipment = GetSiblingComponent<EquipmentComponent>();
            var info = GameBuilder.GameInfo.Instance;
            if (info != null)
            {
                // GameInfo is the PROJECT default — a fallback, not an override. Only seed a value
                // the scene left at its type-default, so a scene-authored FireRate/MoveSpeed (and,
                // at fire time, an equipped weapon's rate) is not clobbered on every scene load.
                if (Mathf.IsEqualApprox(MoveSpeed, 250f)) MoveSpeed = info.MoveSpeed;
                if (Mathf.IsEqualApprox(FireRate, 0.2f)) FireRate = info.FireRate;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _body == null || Engine.IsEditorHint()) return;

            bool isStunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");
            Vector2 input = isStunned ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_up", "move_down");

            // Speed from the entity's "move_speed" stat when it has one (equipment/buffs modify
            // it), else the MoveSpeed export. Same stat channel AttackComponent reads for damage.
            float speed = _stats?.GetValue("move_speed", MoveSpeed) ?? MoveSpeed;
            _body.Velocity = input * speed;
            _body.MoveAndSlide();

            // Aim toward mouse.
            Vector2 mouse = _body.GetGlobalMousePosition();
            _body.Rotation = (mouse - _body.GlobalPosition).Angle();

            // Fire.
            _cooldown -= delta;
            if (Input.IsActionPressed(FireAction) && _cooldown < 0)
            {
                GetViewport().SetInputAsHandled();
                // An equipped weapon drives the fire interval (its Cooldown) and the shot; unarmed
                // uses the controller's own FireRate/ProjectileScene.
                var weapon = _equipment?.MainWeapon;
                _cooldown = weapon != null && weapon.Cooldown > 0f ? weapon.Cooldown : 1.0 / FireRate;
                Vector2 muzzlePos = _muzzle?.GlobalPosition ?? _body.GlobalPosition;
                Vector2 dir = Vector2.FromAngle(_body.Rotation);
                EmitSignal(SignalName.FireFired, muzzlePos, dir);
                SpawnProjectile(muzzlePos, dir, weapon);
            }
        }

        private void SpawnProjectile(Vector2 pos, Vector2 dir, GameWeapon? weapon)
        {
            var scene = weapon?.ProjectileScene ?? ProjectileScene;
            if (scene == null) return;
            var root = GetTree().CurrentScene;
            // Recursive: the Projectiles pool is provided by the LEVEL, which the loader
            // instances under LevelContainer — so it sits at
            // Main/LevelContainer/Level1/Projectiles. A direct child lookup never found it
            // and silently fell back to the scene root, where bullets then outlived the
            // level that spawned them.
            var pool = root.FindChild("Projectiles", recursive: true, owned: false) ?? root;
            var proj = scene.Instantiate();
            pool.AddChild(proj);
            if (proj is Node2D n2d)
            {
                n2d.GlobalPosition = pos;
                n2d.Rotation = dir.Angle();

                var projComp = EntityComponent.FindComponent<ProjectileComponent>(n2d, false);
                if (projComp != null)
                {
                    // Damage from the "damage" stat (equipment contributes the weapon's Damage),
                    // typed by the equipped weapon — the same stat channel AttackComponent reads.
                    projComp.Damage = _stats?.GetValue("damage", ProjectileDamage) ?? ProjectileDamage;
                    projComp.DamageType = weapon?.DamageType ?? DamageType.Physical;
                    projComp.Speed = ProjectileSpeed;
                    // Tell it who fired: it lives under the pool, not under us, so it cannot
                    // infer this — and without it the bullet spawns overlapping our own
                    // hurtbox and damages us on every shot.
                    projComp.Shooter = _body;
                    projComp.Launch(dir);
                }
            }
        }
    }
}
