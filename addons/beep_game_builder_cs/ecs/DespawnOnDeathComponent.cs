using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Frees the entity when its sibling <see cref="HealthComponent"/> dies — the enemy-side
    /// counterpart to <see cref="GameOverOnDeathComponent"/>. <c>HealthComponent.Died</c> only
    /// emits a signal; nothing removed the body, so a dead enemy lingered at 0 HP forever AND
    /// (because it never left the tree) permanently jammed <see cref="SpawnerComponent"/>'s
    /// respawn accounting, which decrements on a spawned node's TreeExiting.
    ///
    /// Attach it to an enemy alongside its HealthComponent. Use <see cref="DespawnDelay"/> to let
    /// a death animation / particle burst play before the body is freed.
    /// (For loot or debris on death, add DropTableComponent / DestructibleComponent — those roll
    /// off the same Died signal and are unaffected by the free here, which is deferred.)
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DespawnOnDeathComponent : GameplayComponent
    {
        /// <summary>Seconds to wait after death before freeing the body (0 = next idle frame).</summary>
        [Export] public float DespawnDelay { get; set; } = 0f;

        [Signal] public delegate void DespawnedEventHandler();

        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null)
            {
                GD.PushWarning($"[{Name}] DespawnOnDeathComponent found no sibling HealthComponent — the body can never despawn. Add it alongside the entity's HealthComponent.");
                return;
            }
            _health.Died += OnDied;
        }

        private void OnDied()
        {
            var body = GetParent();
            if (body == null || !GodotObject.IsInstanceValid(body)) return;

            EmitSignal(SignalName.Despawned);
            if (DespawnDelay <= 0f)
            {
                body.QueueFree();
                return;
            }
            // Delay via a SceneTreeTimer so the wait survives this component being freed with the body.
            var tree = GetTree();
            if (tree == null) { body.QueueFree(); return; }
            var timer = tree.CreateTimer(DespawnDelay);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(body)) body.QueueFree(); };
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.Died -= OnDied;
        }
    }
}
