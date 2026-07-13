using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Destructible object component. Attach to a StaticBody2D or AnimatableBody2D.
    /// Has HP, takes damage, and when destroyed spawns debris + drops.
    /// Pairs with DropTableComponent for loot on break.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DestructibleComponent : WorldComponent
    {
        [Export] public int HP { get; set; } = 1;
        [Export] public PackedScene? DebrisScene { get; set; }
        [Export] public bool DropsOnDestroy { get; set; } = true;
        [Export] public bool DestroyBodyOnBreak { get; set; } = true;

        [Signal] public delegate void DamagedEventHandler(int remainingHP);
        [Signal] public delegate void DestroyedEventHandler(Vector2 position);

        private int _currentHP;

        public override void _Ready()
        {
            base._Ready();
            _currentHP = HP;
        }

        /// <summary>Apply damage. At 0 HP, destroys the object.</summary>
        public void TakeDamage(int amount)
        {
            if (!IsActive || _currentHP <= 0) return;
            _currentHP -= amount;
            EmitSignal(SignalName.Damaged, _currentHP);
            if (_currentHP <= 0) Break();
        }

        private void Break()
        {
            if (GetParent() is not Node2D parent2D) return;

            // Spawn debris.
            if (DebrisScene != null)
            {
                var debris = DebrisScene.Instantiate<Node2D>();
                parent2D.GetParent()?.AddChild(debris);
                debris.GlobalPosition = parent2D.GlobalPosition;
            }

            // Trigger drop table if present.
            if (DropsOnDestroy)
            {
                var dropTable = GetSiblingComponent<DropTableComponent>();
                dropTable?.Roll();
            }

            EmitSignal(SignalName.Destroyed, parent2D.GlobalPosition);

            if (DestroyBodyOnBreak)
                parent2D.QueueFree();
        }
    }
}
