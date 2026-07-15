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

        [ExportGroup("Storm Response")]
        [Export] public bool FleesInStorms { get; set; } = true;
        [Export] public WeatherSystemComponent.WeatherType FleeWeatherType { get; set; } = WeatherSystemComponent.WeatherType.Storm;

        [Signal] public delegate void BehaviorChangedEventHandler(int behavior);
        [Signal] public delegate void HuntedEventHandler();
        [Signal] public delegate void FledEventHandler();

        private BehaviorState _currentBehavior = BehaviorState.Foraging;
        private SeasonalComponent? _seasonal;
        private WeatherSystemComponent? _weather;
        private CharacterBody2D? _body;
        private Vector2 _targetVelocity = Vector2.Zero;

        public override void _Ready()
        {
            base._Ready();
            _seasonal = GetTree().Root.FindChild(nameof(SeasonalComponent), true, false) as SeasonalComponent;
            _weather = GetTree().Root.FindChild(nameof(WeatherSystemComponent), true, false) as WeatherSystemComponent;
            _body = GetParent() as CharacterBody2D;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _body == null) return;

            // Update behavior based on season/weather
            UpdateBehavior();

            // Apply velocity based on current behavior
            _body.Velocity = _targetVelocity;
            if (_body is CharacterBody2D cb) cb.MoveAndSlide();
        }

        private void UpdateBehavior()
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
                BehaviorState.Foraging => Vector2.Zero.Lerp(
                    Vector2.FromAngle((float)GD.Randf() * Mathf.Tau) * ForagingSpeed, 0.1f),
                BehaviorState.Hibernating => Vector2.Zero,
                BehaviorState.Migrating => MigrationDirection.Normalized() * MigrationSpeed,
                BehaviorState.Fleeing => GetFleeDirection() * FleeSpeed,
                BehaviorState.Nesting => Vector2.Zero,
                _ => Vector2.Zero
            };
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
                SeasonalComponent.Season.Fall => BehaviorState.Foraging,       // Pre-migration foraging
                SeasonalComponent.Season.Winter => BehaviorState.Hibernating,  // Dormant
                _ => BehaviorState.Foraging
            };
        }

        private Vector2 GetFleeDirection()
        {
            // Flee in a random direction (away from current position)
            return Vector2.FromAngle((float)GD.Randf() * Mathf.Tau);
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
