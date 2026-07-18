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
        [Export] public PackedScene? SparkScene { get; set; }
        [Export] public Color SparkColor { get; set; } = new(1f, 0.8f, 0.2f, 1f);
        [Export] public float MinDamage { get; set; } = 5f;

        [Signal] public delegate void SparkSpawnedEventHandler(Vector2 position);

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(WireToHealth));
        }

        private void WireToHealth()
        {
            var health = GetSiblingComponent<HealthComponent>();
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDamaged(float amount, float newHealth)
        {
            if (!IsActive || amount < MinDamage) return;
            if (SparkScene == null) return;
            if (GetParent() is not Node2D parent2D) return;

            var spark = SparkScene.Instantiate<Node2D>();
            parent2D.GetParent()?.AddChild(spark);
            spark.GlobalPosition = parent2D.GlobalPosition;
            EmitSignal(SignalName.SparkSpawned, parent2D.GlobalPosition);

            // Auto-free after a delay (particles are one-shot).
            var tree = GetTree();
            if (tree != null)
            {
                var timer = tree.CreateTimer(2f);
                // Capture only the spark, NOT this component (via an instance method): the hit
                // entity may die within 2s, and the timer would then fire into a freed component.
                timer.Timeout += () => { if (GodotObject.IsInstanceValid(spark)) spark.QueueFree(); };
            }
        }

        private void OnSparkTimeout(Node2D spark)
        {
            if (GodotObject.IsInstanceValid(spark))
                spark.QueueFree();
        }

        public override void _ExitTree()
        {
            var health = GetSiblingComponent<HealthComponent>();
            if (health != null)
                health.Damaged -= OnDamaged;
        }
    }
}
