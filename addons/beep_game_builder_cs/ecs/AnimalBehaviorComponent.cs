using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Seasonal animal behavior system. Attach to animal entities (deer, rabbits, birds).
    /// Animals exhibit different behaviors based on season and weather:
    /// - Foraging: normal movement, can be hunted
    /// - Hibernating: stationary, inactive (winter)
    /// - Migrating: moving in a direction (season transitions)
    /// - Fleeing: running from threats (storms, predators)
    /// - Nesting: stationary, reproductive (spring)
    ///
    /// Integrates with SeasonalComponent for season-driven behavior changes
    /// and WeatherSystemComponent for weather-reactive behavior.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AnimalBehaviorComponent : GameplayComponent
    {
        public enum BehaviorState { Foraging, Hibernating, Migrating, Fleeing, Nesting }

        [ExportGroup("Huntability")]
        [Export] public bool CanBeHunted { get; set; } = true;
        [Export] public SeasonalComponent.Season HuntableInSeason { get; set; } = SeasonalComponent.Season.Fall;
        [Export] public float FleeSpeed { get; set; } = 400f;

        [ExportGroup("Behavior")]
        [Export] public float ForagingSpeed { get; set; } = 100f;
        [Export] public float MigrationSpeed { get; set; } = 200f;
        [Export] public Vector2 MigrationDirection { get; set; } = Vector2.Right;
        /// <summary>When true, this animal migrates (moves along <see cref="MigrationDirection"/>)
        /// during Fall. False keeps the old behavior — Fall is just more foraging. Migration was a
        /// defined state nothing ever entered; this opt-in flag is what reaches it.</summary>
        [Export] public bool MigratesInFall { get; set; } = false;
        /// <summary>Seconds between random forage-heading changes (min of the random range).</summary>
        [Export] public float WanderDirectionMinSeconds { get; set; } = 1.5f;
        /// <summary>Seconds between random forage-heading changes (max of the random range).</summary>
        [Export] public float WanderDirectionMaxSeconds { get; set; } = 4f;

        [ExportGroup("Storm Response")]
        [Export] public bool FleesInStorms { get; set; } = true;
        [Export] public WeatherSystemComponent.WeatherType FleeWeatherType { get; set; } = WeatherSystemComponent.WeatherType.Storm;

        [Signal] public delegate void BehaviorChangedEventHandler(int behavior);
        [Signal] public delegate void HuntedEventHandler();

        private BehaviorState _currentBehavior = BehaviorState.Foraging;
        private SeasonalComponent? _seasonal;
        private WeatherSystemComponent? _weather;
        private CharacterBody2D? _body;
        private Vector2 _targetVelocity = Vector2.Zero;
        private float _wanderAngle = 0f;
        private float _wanderTimer = 0f;

        public override void _Ready()
        {
            base._Ready();
            // Group-based discovery (O(1)) over a recursive tree scan. Both systems self-register:
            // WeatherSystemComponent → "weather_system", SeasonalComponent → "seasonal". A recursive
            // FindComponent scan would silently miss a second biome's system.
            _seasonal = FindInGroup<SeasonalComponent>("seasonal");
            _weather = FindInGroup<WeatherSystemComponent>("weather_system");
            _body = GetParent() as CharacterBody2D;

            if (Engine.IsEditorHint()) return;
            if (_body == null)
                GD.PushWarning($"[{Name}] AnimalBehaviorComponent needs a CharacterBody2D parent to move; got '{GetParent()?.GetType().Name ?? "null"}'. The animal will stay inert.");
            if (_seasonal == null)
                GD.PushWarning($"[{Name}] AnimalBehaviorComponent found no SeasonalComponent in the scene; season-driven behavior (nesting/hibernating/foraging) will not change. Add a SeasonalComponent (see atmosphere.tscn).");
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (!IsActive || _body == null) return;

            // Update behavior based on season/weather
            UpdateBehavior(delta);

            // Apply velocity based on current behavior
            _body.Velocity = _targetVelocity;
            _body.MoveAndSlide();
        }

        private void UpdateBehavior(double delta)
        {
            if (_seasonal == null) return;

            BehaviorState newBehavior = DetermineNewBehavior();

            if (newBehavior != _currentBehavior)
            {
                _currentBehavior = newBehavior;
                EmitSignal(SignalName.BehaviorChanged, (int)_currentBehavior);
            }

            // Apply velocity based on behavior
            _targetVelocity = _currentBehavior switch
            {
                BehaviorState.Foraging => GetWanderVelocity(delta),
                BehaviorState.Hibernating => Vector2.Zero,
                BehaviorState.Migrating => MigrationDirection.Normalized() * MigrationSpeed,
                BehaviorState.Fleeing => GetFleeDirection() * FleeSpeed,
                BehaviorState.Nesting => Vector2.Zero,
                _ => Vector2.Zero
            };
        }

        /// <summary>Random-wander forage velocity via the SteeringBehavior wander ring: a point is
        /// projected ahead of the animal and nudged by a random angle on a timer, producing a smooth
        /// meander rather than the old per-frame GD.Randf re-roll (which made the animal vibrate in
        /// place). Eases toward the held heading so turns read as movement, not teleports.</summary>
        private Vector2 GetWanderVelocity(double delta)
        {
            _wanderTimer -= (float)delta;
            Vector2 desired = SteeringBehavior.Wander(
                _targetVelocity, ref _wanderAngle, ForagingSpeed,
                ringDistance: 30f, ringRadius: 20f, jitter: _wanderTimer <= 0f ? 0.9f : 0.1f);
            if (_wanderTimer <= 0f)
                _wanderTimer = (float)GD.RandRange(WanderDirectionMinSeconds, WanderDirectionMaxSeconds);
            return _targetVelocity.Lerp(desired, 0.1f);
        }

        private BehaviorState DetermineNewBehavior()
        {
            if (_seasonal == null) return BehaviorState.Foraging;

            // Storm triggers fleeing
            if (FleesInStorms && _weather?.CurrentWeather == FleeWeatherType)
                return BehaviorState.Fleeing;

            // Season-based behavior
            return _seasonal.CurrentSeason switch
            {
                SeasonalComponent.Season.Spring => BehaviorState.Nesting,      // Reproductive season
                SeasonalComponent.Season.Summer => BehaviorState.Foraging,     // Active foraging
                SeasonalComponent.Season.Fall => MigratesInFall                 // Pre-winter migration
                    ? BehaviorState.Migrating
                    : BehaviorState.Foraging,
                SeasonalComponent.Season.Winter => BehaviorState.Hibernating,  // Dormant
                _ => BehaviorState.Foraging
            };
        }

        /// <summary>First member of the named group that is a T, or null. Group-based discovery is
        /// O(1) against the SceneTree's group index — a recursive FindComponent scan walks the whole
        /// tree and silently misses a second biome's system.</summary>
        private T? FindInGroup<T>(string group) where T : Node
        {
            var tree = GetTree();
            if (tree == null) return null;
            foreach (var n in tree.GetNodesInGroup(group))
                if (n is T hit) return hit;
            return null;
        }

        private Vector2 GetFleeDirection()
        {
            // Flee in a random direction (away from current position)
            return Vector2.FromAngle(GD.Randf() * Mathf.Tau);
        }

        /// <summary>Hunt this animal. Only succeeds during huntable season.</summary>
        public bool TryHunt()
        {
            if (!CanBeHunted || _seasonal == null) return false;
            if (_seasonal.CurrentSeason != HuntableInSeason) return false;

            EmitSignal(SignalName.Hunted);
            return true;
        }

        public BehaviorState GetCurrentBehavior() => _currentBehavior;
        public bool IsHibernating => _currentBehavior == BehaviorState.Hibernating;
        public bool IsNesting => _currentBehavior == BehaviorState.Nesting;
        public bool IsFleeing => _currentBehavior == BehaviorState.Fleeing;
    }
}
