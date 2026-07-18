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
        private InventoryComponent? _inventory;

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            _body = GetParent() as Node2D;
            _equipment = GetSiblingComponent<EquipmentComponent>();
            _inventory = GetSiblingComponent<InventoryComponent>();
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

            // A weapon that eats ammo refuses to fire when there's none, and does NOT start its
            // cooldown (so the trigger can be pulled again the moment ammo arrives).
            var weapon = _equipment?.MainWeapon;
            if (weapon?.AmmoItem != null && !ConsumeAmmo(weapon)) return;

            CooldownRemaining = Cooldown;

            // Damage comes from the entity's "damage" stat when it has one — so equipment and
            // timed buffs modify it — otherwise this component's own Damage export. One stat, read
            // by both damage paths (this and ShooterController), nothing to fork. See StatsComponent.
            var stats = GetSiblingComponent<StatsComponent>();
            float finalDamage = stats?.GetValue("damage", Damage) ?? Damage;

            // The equipped weapon decides the damage type, so a fire sword's hits meet a target's
            // fire resistance. Unarmed (no weapon) falls back to Physical.
            DamageType dtype = weapon?.DamageType ?? DamageType.Physical;

            // An equipped weapon decides whether the attack is ranged and what it fires; unarmed
            // uses this component's own IsRanged/ProjectileScene. (A bow makes you fire even if the
            // component's IsRanged is false.)
            bool ranged = weapon?.IsRanged ?? IsRanged;
            PackedScene? projScene = weapon?.ProjectileScene ?? ProjectileScene;

            if (ranged && projScene != null)
            {
                SpawnProjectile(target, finalDamage, dtype, projScene);
            }
            else if (_body != null)
            {
                DealMeleeDamage(target, finalDamage, dtype);
            }

            EmitSignal(SignalName.Attacked, target, finalDamage);
        }

        private void SpawnProjectile(Vector2 target, float damage, DamageType type, PackedScene scene)
        {
            if (_body == null) return;
            var proj = scene.Instantiate<Node>();
            if (proj is not Node2D projNode) return;

            Vector2 direction = (target - _body.GlobalPosition).Normalized();
            var pool = GetParent()?.GetParent();
            if (pool == null) { proj.QueueFree(); return; }   // nowhere to place it — don't leak an orphan
            pool.AddChild(proj);
            projNode.GlobalPosition = _body.GlobalPosition;   // AFTER parenting, or it's re-derived by the pool's transform

            var projComp = EntityComponent.FindComponent<ProjectileComponent>(projNode, false);
            if (projComp != null)
            {
                projComp.Speed = ProjectileSpeed;
                projComp.Damage = damage;
                projComp.DamageType = type;
                // Without this, IsOwnedByShooter resolves the owner to the pool/level (the
                // projectile's parent), which is an ancestor of every body — so every hit was
                // treated as a self-hit and the shot dealt no damage.
                projComp.Shooter = _body;
                projComp.Launch(direction);
            }
        }

        /// <summary>Spend a weapon's ammo from the wielder's inventory, or refuse. Warns only on
        /// the misconfiguration (ammo required but no inventory) — an empty magazine is a normal
        /// game state, not a bug, so it stays quiet.</summary>
        private bool ConsumeAmmo(GameWeapon weapon)
        {
            if (_inventory == null)
            {
                GD.PushWarning(
                    $"[{Name}] weapon '{weapon.DisplayName}' needs ammo '{weapon.AmmoItem!.Id}' but the " +
                    "wielder has no InventoryComponent — it can never fire. Add an InventoryComponent, or clear GameWeapon.AmmoItem.");
                return false;
            }
            if (!_inventory.HasItem(weapon.AmmoItem!.Id, weapon.AmmoPerUse)) return false;   // out of ammo
            _inventory.RemoveItem(weapon.AmmoItem.Id, weapon.AmmoPerUse);
            return true;
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
