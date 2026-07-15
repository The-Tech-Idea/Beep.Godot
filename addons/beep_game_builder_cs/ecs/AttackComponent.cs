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

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            _body = GetParent() as Node2D;
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

            float finalDamage = Damage;
            var statusEffects = GetSiblingComponent<StatusEffectComponent>();
            if (statusEffects != null)
            {
                float dmgMod = statusEffects.GetModifier("damage_boost", "damage_multiplier", 1f);
                finalDamage *= dmgMod;
            }

            if (IsRanged && ProjectileScene != null)
            {
                SpawnProjectile(target, finalDamage);
            }
            else if (_body != null)
            {
                DealMeleeDamage(target, finalDamage);
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

            var projComp = projNode.FindChild(nameof(ProjectileComponent), false, false) as ProjectileComponent;
            if (projComp != null)
            {
                projComp.Speed = ProjectileSpeed;
                projComp.Damage = damage;
                projComp.Launch(direction);
            }
        }

        private void DealMeleeDamage(Vector2 target, float damage)
        {
            if (_body == null) return;
            var areas = _body.GetWorld2D().DirectSpaceState.IntersectPoint(
                new PhysicsPointQueryParameters2D { Position = target });
            foreach (Godot.Collections.Dictionary result in areas)
            {
                var collider = result["collider"].AsGodotObject() as Node2D;
                if (collider != null && collider != _body)
                {
                    var health = collider.FindChild(nameof(HealthComponent), false, false) as HealthComponent;
                    if (health != null)
                    {
                        health.TakeDamage(damage);
                        var knockback = collider.FindChild(nameof(KnockbackComponent), false, false) as KnockbackComponent;
                        if (knockback != null) knockback.ApplyKnockback(_body.GlobalPosition);
                    }
                }
            }
        }
    }
}
