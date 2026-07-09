using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Attack component for any entity that can deal damage.
    /// Blind — works for player weapons, enemy attacks, traps, or hazards.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AttackComponent : EntityComponent
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

        public void Attack(Vector2 target)
        {
            if (!CanAttack) return;
            CooldownRemaining = Cooldown;
            EmitSignal(SignalName.Attacked, target, Damage);
        }

        public void Tick(double delta)
        {
            if (CooldownRemaining > 0)
            {
                CooldownRemaining -= (float)delta;
                if (CooldownRemaining <= 0) EmitSignal(SignalName.CooldownReady);
            }
        }
    }
}
