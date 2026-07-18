using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Bridges a sibling <see cref="HealthComponent"/>'s death to the run's
    /// <see cref="GameFlowComponent"/>: when the entity dies, it calls
    /// <c>GameFlow.LoseLife()</c>, which decrements Lives and (at zero, when
    /// <c>AutoLoseOnZeroLives</c>) fires GameOver → the game-over / level-failed navigation.
    ///
    /// This is the demonstrated "death ends the run" path — the loss counterpart to
    /// <see cref="PickupComponent"/> demonstrating scoring. Nothing wired
    /// <c>HealthComponent.Died</c> to GameFlow before, so a player reaching 0 HP never ended
    /// the game in ANY genre (GameFlow.GameOver was emitted only from inside LoseLife, which
    /// nothing called). Attach this to the player alongside its HealthComponent.
    ///
    /// With lives remaining, GameFlow just decrements Lives — respawn is the developer's job
    /// (and the planned RespawnComponent's); only the last life triggers GameOver.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameOverOnDeathComponent : GameplayComponent
    {
        /// <summary>Lives removed per death.</summary>
        [Export] public int LivesToLose { get; set; } = 1;

        /// <summary>Optional explicit GameFlow node. Empty = auto-find in the current scene
        /// (GameFlow sits on the main scene while this lives inside the level/player instance,
        /// so a sibling search would miss it — same reason PickupComponent searches the scene).</summary>
        [Export] public NodePath GameFlowPath { get; set; } = new("");

        private HealthComponent? _health;
        private GameFlowComponent? _flow;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null)
            {
                GD.PushWarning($"[{Name}] GameOverOnDeathComponent found no sibling HealthComponent — death can't end the run. Add it alongside the entity's HealthComponent.");
                return;
            }
            _health.Died += OnDied;
        }

        private void OnDied()
        {
            var flow = ResolveGameFlow();
            if (flow != null)
                flow.LoseLife(LivesToLose);
            else
                GD.PushWarning($"[{Name}] '{_health?.Name}' died but no GameFlowComponent was found — the run can't end. Add one to the scene, or set GameFlowPath.");
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
