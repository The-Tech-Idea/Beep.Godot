using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Respawn-at-checkpoint on death — the consumer that closes the loop
    /// <see cref="CheckpointComponent"/> opens. Attach to the player alongside its
    /// <see cref="HealthComponent"/>: when it dies, after a short delay this repositions the body
    /// to <c>GameApp.LastCheckpointPosition</c> (if a checkpoint was reached) and revives it.
    ///
    /// This is the demonstrated respawn path that <see cref="GameOverOnDeathComponent"/>'s doc
    /// refers to as "the planned RespawnComponent". The two compose: put both on the player and
    /// GameOverOnDeath decrements a life on death, while this respawns — UNLESS that was the last
    /// life, in which case a sibling <see cref="GameFlowComponent"/> at 0 lives suppresses the
    /// respawn so game-over wins. With no GameFlow present, it always respawns (endless retries —
    /// the developer's choice by simply not adding GameFlow/lives).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RespawnComponent : GameplayComponent
    {
        /// <summary>Seconds between death and respawn (a beat for a death animation/sound).</summary>
        [Export(PropertyHint.Range, "0,10,0.1")] public float RespawnDelay { get; set; } = 0.8f;

        /// <summary>Health to revive at (default full).</summary>
        [Export] public float ReviveHealth { get; set; } = -1f;

        /// <summary>Optional explicit GameFlow node (else auto-found). Used only to check remaining
        /// lives — at 0 lives this stands down so game-over wins.</summary>
        [Export] public NodePath GameFlowPath { get; set; } = new("");

        [Signal] public delegate void RespawnedEventHandler(Vector2 position);

        private HealthComponent? _health;
        private GameFlowComponent? _flow;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null)
            {
                GD.PushWarning($"[{Name}] RespawnComponent found no sibling HealthComponent — it can never respawn anything. Add it alongside the entity's HealthComponent.");
                return;
            }
            _health.Died += OnDied;
        }

        private void OnDied()
        {
            // Defer the respawn: this also lets any sibling GameOverOnDeathComponent run its
            // LoseLife() synchronously first, so the lives check below sees the post-death count.
            var tree = GetTree();
            if (tree == null) { Respawn(); return; }
            var timer = tree.CreateTimer(Mathf.Max(0f, RespawnDelay));
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(this)) Respawn(); };
        }

        private void Respawn()
        {
            if (!IsActive || _health == null) return;

            // Last life → let game-over happen; don't yank the player back. No GameFlow → always respawn.
            var flow = ResolveGameFlow();
            if (flow != null && flow.Lives <= 0)
                return;

            if (GetParent() is not Node2D body)
            {
                GD.PushWarning($"[{Name}] RespawnComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Node2D — it can revive health but cannot reposition. Parent it under the player body.");
                _health.Revive(ReviveHealth);
                return;
            }

            // Reposition to the last checkpoint, if one was reached (LastCheckpointLevel starts -1).
            var app = GameApp.Instance;
            if (app != null && app.LastCheckpointLevel >= 0)
                body.GlobalPosition = app.LastCheckpointPosition;

            _health.Revive(ReviveHealth);
            EmitSignal(SignalName.Respawned, body.GlobalPosition);
        }

        private GameFlowComponent? ResolveGameFlow()
        {
            if (_flow != null && GodotObject.IsInstanceValid(_flow)) return _flow;
            if (!GameFlowPath.IsEmpty) _flow = GetNodeOrNull<GameFlowComponent>(GameFlowPath);
            if (_flow == null && GetTree()?.CurrentScene is { } scene)
                _flow = EntityComponent.FindComponent<GameFlowComponent>(scene, true);
            return _flow;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.Died -= OnDied;
        }
    }
}
