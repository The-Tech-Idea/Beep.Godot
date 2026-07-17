using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Attack component for any entity that can deal damage.
    /// Blind — works for player weapons, enemy attacks, traps, or hazards.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AttackComponent : GameplayComponent
    {
        [Export] public float Damage { get; set; } = 10f;
        [Export] public float Range { get; set; } = 50f;
        [Export] public float Cooldown { get; set; } = 0.5f;
        [Export] public bool IsRanged { get; set; } = false;
        [Export] public float ProjectileSpeed { get; set; } = 400f;
        [Export] public PackedScene? ProjectileScene { get; set; }

        [Signal] public delegate void AttackedEventHandler(Vector2 target, float damage);
        [Signal] public delegate void CooldownReadyEventHandler();

        public float CooldownRemaining { get; private set; }
        public bool CanAttack => CooldownRemaining <= 0 && IsActive;

        private HealthComponent? _health;
        private Node2D? _body;
        private EquipmentComponent? _equipment;

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            _body = GetParent() as Node2D;
            _equipment = GetSiblingComponent<EquipmentComponent>();
        }

        public override void _Process(double delta)
        {
            if (CooldownRemaining > 0)
            {
                CooldownRemaining -= (float)delta;
                if (CooldownRemaining <= 0) EmitSignal(SignalName.CooldownReady);
            }
        }

        public void Attack(Vector2 target)
        {
            if (!CanAttack) return;
            CooldownRemaining = Cooldown;

            // Damage comes from the entity's "damage" stat when it has one — so equipment and
            // timed buffs modify it — otherwise this component's own Damage export. One stat, read
            // by both damage paths (this and ShooterController), nothing to fork. See StatsComponent.
            var stats = GetSiblingComponent<StatsComponent>();
            float finalDamage = stats?.GetValue("damage", Damage) ?? Damage;

            // The equipped weapon decides the damage type, so a fire sword's hits meet a target's
            // fire resistance. Unarmed (no weapon) falls back to Physical.
            DamageType dtype = _equipment?.MainWeapon?.DamageType ?? DamageType.Physical;

            if (IsRanged && ProjectileScene != null)
            {
                SpawnProjectile(target, finalDamage);
            }
            else if (_body != null)
            {
                DealMeleeDamage(target, finalDamage, dtype);
            }

            EmitSignal(SignalName.Attacked, target, finalDamage);
        }

        private void SpawnProjectile(Vector2 target, float damage)
        {
            if (_body == null || ProjectileScene == null) return;
            var proj = ProjectileScene.Instantiate<Node>();
            if (proj is not Node2D projNode) return;

            projNode.GlobalPosition = _body.GlobalPosition;
            Vector2 direction = (target - _body.GlobalPosition).Normalized();
            GetParent()?.GetParent()?.AddChild(proj);

            var projComp = EntityComponent.FindComponent<ProjectileComponent>(projNode, false);
            if (projComp != null)
            {
                projComp.Speed = ProjectileSpeed;
                projComp.Damage = damage;
                projComp.Launch(direction);
            }
        }

        private void DealMeleeDamage(Vector2 target, float damage, DamageType type)
        {
            if (_body == null) return;
            var areas = _body.GetWorld2D().DirectSpaceState.IntersectPoint(
                new PhysicsPointQueryParameters2D { Position = target });
            foreach (Godot.Collections.Dictionary result in areas)
            {
                var collider = result["collider"].AsGodotObject() as Node2D;
                if (collider != null && collider != _body)
                {
                    var health = EntityComponent.FindComponent<HealthComponent>(collider, false);
                    if (health != null)
                    {
                        health.TakeDamage(new GameDamage(damage, type, _body));
                        var knockback = EntityComponent.FindComponent<KnockbackComponent>(collider, false);
                        if (knockback != null) knockback.ApplyKnockback(_body.GlobalPosition);
                    }
                }
            }
        }
    }
}
