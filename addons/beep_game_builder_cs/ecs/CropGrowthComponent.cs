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
        private DropTableComponent? _dropTable;
        private ColorRect? _visual;

        public override void _Ready()
        {
            base._Ready();
            // Auto-discover seasonal system
            var root = GetTree().Root;
            _seasonal = root.FindChild(nameof(SeasonalComponent), true, false) as SeasonalComponent;
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
            if (!IsActive || _seasonal == null || _currentStage == GrowthStage.Harvested) return;

            // Apply seasonal growth multiplier
            float growthRate = GetSeasonalGrowthRate();
            if (growthRate <= 0) return;  // No growth in off-season

            // Simulate 1 in-game day as unit of growth
            // Assuming DayNightCycleComponent runs ~120 seconds per in-game day
            float dailyProgress = growthRate / (DaysToMaturity * 120f);  // Normalized to ~2min per day
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

            // Schedule reset to sprout after short delay
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                _currentStage = GrowthStage.Sprout;
                _growthProgress = 0f;
                UpdateVisual();
            });
        }

        public GrowthStage GetCurrentStage() => _currentStage;
        public float GetGrowthProgress() => _growthProgress;

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
