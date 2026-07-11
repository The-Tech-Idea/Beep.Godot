using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Checkpoint/respawn point. Attach to an Area2D. When a body enters, it becomes
    /// the active respawn point (stored on GameApp). Emits CheckpointActivated.
    /// Pair with a HealthComponent + GameApp for death-respawn: on death, respawn
    /// at GameApp's stored checkpoint position.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CheckpointComponent : WorldComponent
    {
        /// <summary>If true, heal the entering body to full (classic checkpoint behavior).</summary>
        [Export] public bool HealOnActivate { get; set; } = true;

        /// <summary>If true, deactivate after one use (linear progression).</summary>
        [Export] public bool SingleUse { get; set; } = false;

        [Signal] public delegate void CheckpointActivatedEventHandler(Vector2 position);

        private bool _activated;
        private Area2D? _area;

        public override void _Ready()
        {
            base._Ready();
            _area = GetParent() as Area2D;
            if (_area != null)
                _area.BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node body)
        {
            if (!IsActive || _activated) return;
            _activated = true;

            Vector2 pos = _area?.GlobalPosition ?? Vector2.Zero;
            var app = GameApp.Instance;
            if (app != null) app.SetLevel(app.CurrentLevel); // mark progression

            if (HealOnActivate && body is Node2D n && n.HasNode("HealthComponent"))
                if (n.GetNode<HealthComponent>("HealthComponent") is { } h) h.Heal(h.MaxHealth);

            EmitSignal(SignalName.CheckpointActivated, pos);

            if (SingleUse && _area != null)
                _area.Monitoring = false;
        }
    }
}
