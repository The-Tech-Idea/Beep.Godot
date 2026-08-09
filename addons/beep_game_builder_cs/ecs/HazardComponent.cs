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

        [ExportGroup("Height (2.5D)")]
        /// <summary>Whether this hazard sits on the ground (spikes, lava) or in the air (a flak
        /// cloud, a gas jet at head height). Grounded hazards only hit grounded/low targets — a
        /// flying enemy passes OVER spikes. Airborne hazards only hit flyers.</summary>
        [Export] public bool IsGroundHazard { get; set; } = true;
        /// <summary>The hazard's own height band center (px). 0 = on the ground plane.</summary>
        [Export] public float HazardHeight { get; set; } = 0f;
        /// <summary>How tall the hazard's band is — a floor of lava is thin; a gas cloud is tall.
        /// A target is hit only when its height band overlaps [HazardHeight ± HazardHalfThickness].</summary>
        [Export] public float HazardHalfThickness { get; set; } = 24f;
        /// <summary>When false, height is ignored entirely (legacy flat behavior — every body in the
        /// Area2D is hit). Turn on for 2.5D games using HeightComponent.</summary>
        [Export] public bool RespectHeight { get; set; } = false;

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
            if (!HeightGatePasses(body)) return;
            var health = EntityComponent.FindComponent<HealthComponent>(body, false);
            if (health == null) return;   // a body with no health simply isn't hurt by the hazard
            health.TakeDamage(new GameDamage(Damage, DamageType, TriggerArea));
            EmitSignal(SignalName.HazardHit, body, Damage);
        }

        /// <summary>2.5D hit gate. With <see cref="RespectHeight"/> on, a body is hit only when its
        /// height band overlaps the hazard's band — so a flyer clears spikes and a walker wades under
        /// a flak cloud. A body with no HeightComponent is treated as grounded (Height 0), matching
        /// how the ground plane reads. Off = legacy flat behavior (every body hit).</summary>
        private bool HeightGatePasses(Node2D body)
        {
            if (!RespectHeight) return true;
            // IsGroundHazard picks the default band: ground hazards sit at 0, airborne ones default
            // to a high band when HazardHeight wasn't set above 0. An explicit HazardHeight overrides.
            float band = HazardHeight;
            if (!IsGroundHazard && band <= 0f) band = 128f;   // a default "in the air" altitude
            var height = EntityComponent.FindComponent<HeightComponent>(body, false);
            if (height == null)
                // Grounded body: its band is [0 ± 16]. Overlap with the hazard's band decides.
                return Mathf.Abs(band - 0f) <= HazardHalfThickness + 16f;
            return height.HeightOverlaps(band, HazardHalfThickness);
        }
    }
}
