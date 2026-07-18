using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Damage-on-contact hazard — spikes, lava, a poison cloud, a damaging trap. Attach to an
    /// Area2D; a body that enters (and, unless <see cref="DamageOnce"/>, keeps standing in it) takes
    /// typed <see cref="GameDamage"/>.
    ///
    /// This is the "damage on contact" primitive the framework was missing, and it is deliberately
    /// tiny: <see cref="AreaTriggerComponent"/> already does the safe Area2D body-trigger (resolve +
    /// warn on a wrong parent), and the GameDamage packet already carries type and source — so a
    /// hazard's hits meet a target's ResistanceComponent exactly like a weapon's do.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HazardComponent : AreaTriggerComponent
    {
        [Export] public float Damage { get; set; } = 10f;
        [Export] public DamageType DamageType { get; set; } = DamageType.Physical;

        /// <summary>Deal damage once on entry only. When false, a body standing in the hazard is hit
        /// again every <see cref="TickInterval"/> seconds.</summary>
        [Export] public bool DamageOnce { get; set; } = false;

        /// <summary>Seconds between repeat hits while a body stays inside (ignored if
        /// <see cref="DamageOnce"/>).</summary>
        [Export] public float TickInterval { get; set; } = 0.5f;

        [Signal] public delegate void HazardHitEventHandler(Node2D body, float amount);

        private readonly List<Node2D> _inside = new();
        private float _tickTimer;

        protected override void OnBodyEntered(Node2D body)
        {
            Hit(body);
            if (!DamageOnce && !_inside.Contains(body)) _inside.Add(body);
        }

        protected override void OnBodyExited(Node2D body) => _inside.Remove(body);

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || DamageOnce || _inside.Count == 0) return;
            _tickTimer += (float)delta;
            if (_tickTimer < TickInterval) return;
            _tickTimer = 0f;
            // Iterate a copy backwards: a hit can free the body (Died → QueueFree), and OnBodyExited
            // removes it — mutating the list mid-iteration.
            for (int i = _inside.Count - 1; i >= 0; i--)
            {
                var body = _inside[i];
                if (!GodotObject.IsInstanceValid(body)) { _inside.RemoveAt(i); continue; }
                Hit(body);
            }
        }

        private void Hit(Node2D body)
        {
            if (!IsActive) return;
            var health = EntityComponent.FindComponent<HealthComponent>(body, false);
            if (health == null) return;   // a body with no health simply isn't hurt by the hazard
            health.TakeDamage(new GameDamage(Damage, DamageType, TriggerArea));
            EmitSignal(SignalName.HazardHit, body, Damage);
        }
    }
}
