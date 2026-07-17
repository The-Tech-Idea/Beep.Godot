using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Destructible object. Attach to a StaticBody2D/AnimatableBody2D <b>alongside a
    /// HealthComponent</b>; when that health reaches 0 the object breaks — spawns debris and
    /// (optionally) frees the body. For loot, add a DropTableComponent: it rolls off the same
    /// Died, independently of this.
    ///
    /// It no longer carries its own HP. It used to keep a private <c>int</c> pool with its own
    /// <c>TakeDamage(int)</c>, which no damage source ever called — AttackComponent and
    /// ProjectileComponent resolve only HealthComponent — so every destructible in every genre
    /// was invulnerable. Unified behind HealthComponent, every existing damage source reaches it
    /// for free, and <c>Died → Break()</c> is the entry point.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DestructibleComponent : WorldComponent
    {
        [Export] public PackedScene? DebrisScene { get; set; }
        [Export] public bool DestroyBodyOnBreak { get; set; } = true;

        [Signal] public delegate void DestroyedEventHandler(Vector2 position);

        private HealthComponent? _health;
        private bool _broken;

        public override void _Ready()
        {
            base._Ready();
            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null)
            {
                GD.PushWarning(
                    $"[{Name}] (DestructibleComponent): no sibling HealthComponent — this object " +
                    "has no health pool and can never break. Add a HealthComponent to the same body.");
                return;
            }
            _health.Died += Break;
        }

        /// <summary>Break the object: debris, drops, then optionally free the body. Idempotent —
        /// Died fires once, and the latch also guards a manual <see cref="Break"/> call.</summary>
        public void Break()
        {
            if (_broken) return;
            _broken = true;

            if (GetParent() is not Node2D parent2D) return;

            if (DebrisScene != null)
            {
                var debris = DebrisScene.Instantiate<Node2D>();
                parent2D.GetParent()?.AddChild(debris);
                debris.GlobalPosition = parent2D.GlobalPosition;
            }

            // Loot is not this component's job — a sibling DropTableComponent rolls off the same
            // HealthComponent.Died, so a destructible-with-loot drops once, not twice.
            EmitSignal(SignalName.Destroyed, parent2D.GlobalPosition);

            if (DestroyBodyOnBreak)
                parent2D.QueueFree();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null) _health.Died -= Break;
        }
    }
}
