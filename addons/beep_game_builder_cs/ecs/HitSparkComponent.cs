using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Particle burst on damage impact. Listens to a sibling HealthComponent's
    /// Damaged signal and spawns a particle effect at the collision point.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HitSparkComponent : WorldComponent
    {
        // Shipped fallback so a hit still sparks with no per-scene wiring. Assign SparkScene to override.
        private const string DefaultSparkScenePath = "res://addons/beep_game_builder_cs/templates/particles/hit_sparks.tscn";

        [Export] public PackedScene? SparkScene { get; set; }
        [Export] public Color SparkColor { get; set; } = new(1f, 0.8f, 0.2f, 1f);
        [Export] public float MinDamage { get; set; } = 5f;

        [Signal] public delegate void SparkSpawnedEventHandler(Vector2 position);

        private HealthComponent? _health;
        private PackedScene? _sparkScene;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            Callable.From(WireToHealth).CallDeferred();
        }

        private void WireToHealth()
        {
            // Resolve the effective spark scene once: explicit export, else the shipped default.
            _sparkScene = SparkScene
                ?? (ResourceLoader.Exists(DefaultSparkScenePath) ? ResourceLoader.Load<PackedScene>(DefaultSparkScenePath) : null);
            if (_sparkScene == null)
                GD.PushWarning($"[{Name}] HitSparkComponent has no SparkScene and the shipped default could not load — hits will not spark.");

            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null)
                _health.Damaged += OnDamaged;
            else
                // Entirely signal-driven — with no Health sibling it never sparks and used to say
                // nothing. Add it beside a HealthComponent on the same entity.
                GD.PushWarning($"[{Name}] HitSparkComponent found no sibling HealthComponent — it will never spark. Add it beside a HealthComponent.");
        }

        private void OnDamaged(float amount, float newHealth)
        {
            if (!IsActive || amount < MinDamage) return;
            if (_sparkScene == null) return;
            if (GetParent() is not Node2D parent2D) return;

            var spark = _sparkScene.InstantiateOrNull<Node2D>();
            if (spark == null)
            {
                GD.PushWarning($"[{Name}] HitSparkComponent's SparkScene '{_sparkScene.ResourcePath}' does not root a Node2D — cannot spawn it.");
                return;
            }
            parent2D.GetParent()?.AddChild(spark);
            spark.GlobalPosition = parent2D.GlobalPosition;
            EmitSignal(SignalName.SparkSpawned, parent2D.GlobalPosition);

            // Auto-free after a delay (particles are one-shot).
            var tree = GetTree();
            if (tree != null)
            {
                var timer = tree.CreateTimer(2f);
                // Capture only the spark, NOT this component (via a closure over the local): the hit
                // entity may die within 2s, and the timer would then fire into a freed component.
                timer.Timeout += () => { if (GodotObject.IsInstanceValid(spark)) spark.QueueFree(); };
            }
        }

        public override void _ExitTree()
        {
            if (_health != null)
                _health.Damaged -= OnDamaged;
        }
    }
}
