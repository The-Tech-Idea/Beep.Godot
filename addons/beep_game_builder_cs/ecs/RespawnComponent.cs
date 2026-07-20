using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Respawn-at-checkpoint on death — the consumer that closes the loop
    /// <see cref="CheckpointComponent"/> opens. Attach to the player alongside its
    /// <see cref="HealthComponent"/>: when it dies, after a short delay this repositions the body
    /// to the reached checkpoint (<c>GameApp.LastCheckpointPosition</c> when a checkpoint has been
    /// hit) — or, before any checkpoint, back to the entity's captured spawn position — and revives
    /// it. It never revives in place at the death spot (which would be a re-death loop in a pit).
    ///
    /// This is the demonstrated respawn path <see cref="GameOverOnDeathComponent"/>'s doc calls "the
    /// planned RespawnComponent". They compose: put BOTH on the player — GameOverOnDeath decrements a
    /// life on death (it is the only thing that lowers <see cref="GameFlowComponent.Lives"/>), while
    /// this respawns. On the last life it stands down so game-over wins — but only when GameFlow will
    /// actually end the run (<see cref="GameFlowComponent.AutoLoseOnZeroLives"/>); if that's off, it
    /// respawns anyway rather than leave the player stranded dead. With no GameFlow / no
    /// GameOverOnDeath present, lives never fall, so it respawns endlessly (the developer's choice).
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
        private Vector2 _spawnPosition;
        private bool _haveSpawnPosition;

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
            // Capture the entity's authored spawn position, deferred so its parent is placed in the
            // level first. This is the fallback respawn point when no checkpoint has been reached —
            // without it, "respawn" revived the player where it died (in the pit/spike that killed it).
            Callable.From(CaptureSpawnPosition).CallDeferred();
        }

        private void CaptureSpawnPosition()
        {
            if (GetParent() is Node2D body)
            {
                _spawnPosition = body.GlobalPosition;
                _haveSpawnPosition = true;
            }
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

            // Stand down at 0 lives ONLY when game-over will actually fire (GameFlow will navigate
            // away). If AutoLoseOnZeroLives is off, nothing ends the run, so respawning anyway is the
            // right call — otherwise the player is stranded dead with no respawn and no game-over.
            var flow = ResolveGameFlow();
            if (flow != null && flow.Lives <= 0 && flow.AutoLoseOnZeroLives)
                return;

            if (GetParent() is not Node2D body)
            {
                GD.PushWarning($"[{Name}] RespawnComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Node2D — it can revive health but cannot reposition. Parent it under the player body.");
                _health.Revive(ReviveHealth);
                return;
            }

            // Reposition to the reached checkpoint (HasQuicksave is set ONLY by SetCheckpoint, so it
            // cleanly means "a checkpoint exists" — unlike LastCheckpointLevel, which mirrors
            // CurrentLevel and stays -1 in genres that don't route through level-select). With no
            // checkpoint yet, fall back to the captured spawn point — never revive in the death spot.
            var app = GameApp.Instance;
            if (app != null && app.HasQuicksave)
                body.GlobalPosition = app.LastCheckpointPosition;
            else if (_haveSpawnPosition)
                body.GlobalPosition = _spawnPosition;

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
