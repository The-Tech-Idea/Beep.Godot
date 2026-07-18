using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Seasonal crop growth system. Attach to a Node2D for farmable terrain/crops.
    /// Crops progress through growth stages (Sprout → Growing → Mature → Harvestable).
    /// Growth speed varies by season. Harvest produces items via DropTableComponent.
    /// Supports multiple crop types with different growth times.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CropGrowthComponent : GameplayComponent
    {
        public enum GrowthStage { Sprout, Growing, Mature, Harvestable, Harvested }

        [ExportGroup("Crop Config")]
        [Export] public string CropType { get; set; } = "wheat";
        [Export] public float DaysToMaturity { get; set; } = 10f;
        [Export] public Color SproutColor { get; set; } = new(0.2f, 0.6f, 0.2f, 1f);
        [Export] public Color GrowingColor { get; set; } = new(0.4f, 0.8f, 0.3f, 1f);
        [Export] public Color MatureColor { get; set; } = new(0.8f, 0.9f, 0.2f, 1f);
        [Export] public Color HarvestableColor { get; set; } = new(1f, 0.8f, 0.2f, 1f);

        [ExportGroup("Season Multipliers")]
        [Export] public float SpringGrowthRate { get; set; } = 1.5f;  // 150% growth in spring
        [Export] public float SummerGrowthRate { get; set; } = 1.0f;  // Normal growth in summer
        [Export] public float FallGrowthRate { get; set; } = 0.5f;    // 50% growth in fall
        [Export] public float WinterGrowthRate { get; set; } = 0.0f;  // No growth in winter

        [Signal] public delegate void GrowthStageChangedEventHandler(int stage, float progress);
        [Signal] public delegate void CropReadyForHarvestEventHandler();
        [Signal] public delegate void CropHarvestedEventHandler();

        private GrowthStage _currentStage = GrowthStage.Sprout;
        private float _growthProgress = 0f;  // 0-1, where 1.0 = maturity
        private SeasonalComponent? _seasonal;
        private DayNightCycleComponent? _dayNight;
        private WeatherSystemComponent? _weather;
        private DropTableComponent? _dropTable;
        private ColorRect? _visual;
        private bool _warnedNoSeasonal;

        public override void _Ready()
        {
            base._Ready();
            // Runtime only: this spawns a ColorRect visual into the PARENT and starts the
            // growth timer. This is [Tool], so without the guard, opening a scene that uses
            // it would add runtime-only nodes to the parent in the editor.
            if (Engine.IsEditorHint()) return;

            // Auto-discover the atmosphere systems that modulate growth: season (rate), the
            // day/night clock (its DayLengthSeconds is the real day length — was hardcoded 120),
            // and weather (rain/storm accelerate, snow slows).
            var root = GetTree().Root;
            _seasonal = EntityComponent.FindComponent<SeasonalComponent>(root, true);
            _dayNight = EntityComponent.FindComponent<DayNightCycleComponent>(root, true);
            _weather = EntityComponent.FindComponent<WeatherSystemComponent>(root, true);
            _dropTable = GetSiblingComponent<DropTableComponent>();

            // Create visual representation if not present
            if (GetParent() is not Node2D parent) return;
            _visual = parent.GetNodeOrNull<ColorRect>("CropVisual");
            if (_visual == null)
            {
                _visual = new ColorRect
                {
                    Name = "CropVisual",
                    CustomMinimumSize = new Vector2(32, 32),
                    Color = SproutColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                parent.AddChild(_visual);
            }
            UpdateVisual();
        }

        public override void _Process(double delta)
        {
            // Stop once ready (Harvestable) or picked (Harvested): a ripe crop must not keep
            // accumulating, or it re-crosses 1.0 each cycle and re-fires CropReadyForHarvest forever.
            if (!IsActive ||
                _currentStage == GrowthStage.Harvestable || _currentStage == GrowthStage.Harvested) return;

            if (_seasonal == null && !_warnedNoSeasonal)
            {
                _warnedNoSeasonal = true;
                GD.PushWarning($"[{Name}] No SeasonalComponent found — the crop grows at a flat rate (no seasonal slowdown). Add the atmosphere (SeasonalComponent) for season-driven growth.");
            }

            // Season sets the base rate (winter = 0); weather scales it (rain/storm accelerate,
            // snow slows). Off-season with no rain means no growth.
            float growthRate = GetSeasonalGrowthRate() * WeatherGrowthMultiplier();
            if (growthRate <= 0) return;

            // One unit of growth == DaysToMaturity in-game days, timed off the ACTUAL day length
            // (DayNightCycleComponent.DayLengthSeconds) rather than a hardcoded 120.
            float dayLength = _dayNight?.DayLengthSeconds ?? 120f;
            float dailyProgress = growthRate / (DaysToMaturity * dayLength);
            _growthProgress += (float)delta * dailyProgress;

            // Check for stage advancement
            CheckAndAdvanceStage();
            UpdateVisual();
        }

        private void CheckAndAdvanceStage()
        {
            if (_growthProgress >= 1.0f)
            {
                _growthProgress = 0f;
                _currentStage = _currentStage switch
                {
                    GrowthStage.Sprout => GrowthStage.Growing,
                    GrowthStage.Growing => GrowthStage.Mature,
                    GrowthStage.Mature => GrowthStage.Harvestable,
                    _ => GrowthStage.Harvestable
                };

                EmitSignal(SignalName.GrowthStageChanged, (int)_currentStage, 0f);

                if (_currentStage == GrowthStage.Harvestable)
                    EmitSignal(SignalName.CropReadyForHarvest);
            }
            else
            {
                EmitSignal(SignalName.GrowthStageChanged, (int)_currentStage, _growthProgress);
            }
        }

        /// <summary>Harvest the crop, spawn items, reset to sprout.</summary>
        public void Harvest()
        {
            if (_currentStage != GrowthStage.Harvestable) return;

            _dropTable?.Roll();
            _currentStage = GrowthStage.Harvested;
            EmitSignal(SignalName.CropHarvested);

            // Schedule reset to sprout after short delay.
            // Uses a SceneTree timer, not Task.Delay: the callback touches node state and
            // must run on the main thread.
            var timer = GetTree()?.CreateTimer(1.0);
            if (timer != null)
                timer.Timeout += () =>
                {
                    _currentStage = GrowthStage.Sprout;
                    _growthProgress = 0f;
                    UpdateVisual();
                };
        }

        public GrowthStage GetCurrentStage() => _currentStage;
        public float GetGrowthProgress() => _growthProgress;

        /// <summary>Weather scales growth: rain waters the crop (faster), snow/hail freezes it,
        /// sandstorm parches it. Clear/cloudy/fog are neutral. 1.0 when no weather system.</summary>
        private float WeatherGrowthMultiplier()
        {
            if (_weather == null) return 1f;
            return _weather.CurrentWeather switch
            {
                WeatherSystemComponent.WeatherType.Rain => 1.4f,
                WeatherSystemComponent.WeatherType.Storm => 1.2f,
                WeatherSystemComponent.WeatherType.Snow or WeatherSystemComponent.WeatherType.Hail => 0.4f,
                WeatherSystemComponent.WeatherType.Sandstorm => 0.6f,
                _ => 1f
            };
        }

        private float GetSeasonalGrowthRate()
        {
            if (_seasonal == null) return 1.0f;

            return _seasonal.CurrentSeason switch
            {
                SeasonalComponent.Season.Spring => SpringGrowthRate,
                SeasonalComponent.Season.Summer => SummerGrowthRate,
                SeasonalComponent.Season.Fall => FallGrowthRate,
                SeasonalComponent.Season.Winter => WinterGrowthRate,
                _ => 1.0f
            };
        }

        private void UpdateVisual()
        {
            if (_visual == null) return;

            Color targetColor = _currentStage switch
            {
                GrowthStage.Sprout => SproutColor,
                GrowthStage.Growing => GrowingColor,
                GrowthStage.Mature => MatureColor,
                GrowthStage.Harvestable => HarvestableColor,
                _ => SproutColor
            };

            _visual.Color = targetColor;

            // Scale visual based on growth
            float scale = 0.5f + (_growthProgress * 0.5f);  // Grow from 50% to 100% size
            _visual.Scale = new Vector2(scale, scale);
        }
    }
}
