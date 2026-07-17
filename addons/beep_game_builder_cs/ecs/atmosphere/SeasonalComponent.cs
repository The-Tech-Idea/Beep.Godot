using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Seasonal system with visual transitions. Controls foliage color, wind effects,
    /// and environment tinting across Spring/Summer/Fall/Winter.
    ///
    /// Attach to a Node2D world root. Seasons rotate on a configurable cycle or can
    /// be set manually. Uses Canvas Item shaders for color blending and foliage wind.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SeasonalComponent : WorldComponent
    {
        public enum Season
        {
            Spring,
            Summer,
            Fall,
            Winter
        }

        [ExportGroup("General")]
        [Export] public Season CurrentSeason { get; set; } = Season.Spring;
        [Export] public bool AutoCycle { get; set; } = true;
        [Export] public double DaysPerSeason { get; set; } = 7.0;  // in-game days

        [ExportGroup("Seasonal Colors")]
        [Export] public Color SpringColor { get; set; } = new(0.4f, 0.8f, 0.4f, 1f);
        [Export] public Color SummerColor { get; set; } = new(0.3f, 0.9f, 0.3f, 1f);
        [Export] public Color FallColor { get; set; } = new(0.9f, 0.6f, 0.2f, 1f);
        [Export] public Color WinterColor { get; set; } = new(0.8f, 0.8f, 0.95f, 1f);

        [ExportGroup("Foliage Wind")]
        [Export] public float SpringWindSpeed { get; set; } = 1.5f;
        [Export] public float SummerWindSpeed { get; set; } = 1.0f;
        [Export] public float FallWindSpeed { get; set; } = 2.0f;
        [Export] public float WinterWindSpeed { get; set; } = 0.5f;
        [Export] public float FoliageWindStrength { get; set; } = 0.3f;

        [ExportGroup("Transitions")]
        [Export] public float TransitionDuration { get; set; } = 3.0f;

        [Signal] public delegate void SeasonChangedEventHandler(int season);

        private Tween? _seasonTransitionTween;
        private Color _currentSeasonColor;
        // The day/night clock is the source of in-game days. Seasons advance every DaysPerSeason
        // in-game days — NOT every DaysPerSeason real seconds, which is what the old _seasonTimer
        // (real delta compared against a "days" value) did: seasons rotated every 7 seconds.
        private DayNightCycleComponent? _dayNight;
        private int _seasonStartDay;
        private bool _warnedNoClock;

        public override void _Ready()
        {
            base._Ready();
            _currentSeasonColor = GetColorForSeason(CurrentSeason);
            if (!IsInGroup("seasonal")) AddToGroup("seasonal");
            if (Beep.GameBuilder.GameInfo.Instance is { } info) IsActive = info.EnableSeasons;
            CallDeferred(nameof(DeferredInit));
        }

        private void DeferredInit()
        {
            if (Engine.IsEditorHint()) return;
            _dayNight = EntityComponent.FindComponent<DayNightCycleComponent>(GetTree().Root, true);
            _seasonStartDay = _dayNight?.DaysElapsed ?? 0;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint() || !AutoCycle) return;

            if (_dayNight == null)
            {
                if (!_warnedNoClock)
                {
                    _warnedNoClock = true;
                    GD.PushWarning(
                        $"[{Name}] AutoCycle is on but there is no DayNightCycleComponent in the tree — " +
                        "seasons derive from in-game days and cannot advance without it. Add a " +
                        "DayNightCycleComponent, or set seasons manually via SetSeason().");
                }
                return;
            }

            if (_dayNight.DaysElapsed - _seasonStartDay >= DaysPerSeason)
            {
                _seasonStartDay = _dayNight.DaysElapsed;
                SetSeason((Season)(((int)CurrentSeason + 1) % 4));
            }
        }

        /// <summary>
        /// Transition to a new season with smooth color blending.
        /// </summary>
        public void SetSeason(Season newSeason)
        {
            if (CurrentSeason == newSeason) return;

            CurrentSeason = newSeason;
            // Reset the day marker so a manual SetSeason() also restarts the season's day count.
            _seasonStartDay = _dayNight?.DaysElapsed ?? _seasonStartDay;

            Color targetColor = GetColorForSeason(newSeason);
            _seasonTransitionTween?.Kill();
            _seasonTransitionTween = CreateTween();
            _seasonTransitionTween.SetTrans(Tween.TransitionType.Sine);
            _seasonTransitionTween.TweenMethod(
                Callable.From<Color>(c => _currentSeasonColor = c),
                _currentSeasonColor,
                targetColor,
                TransitionDuration
            );

            EmitSignal(SignalName.SeasonChanged, (int)newSeason);
        }

        private Color GetColorForSeason(Season season) => season switch
        {
            Season.Spring => SpringColor,
            Season.Summer => SummerColor,
            Season.Fall => FallColor,
            Season.Winter => WinterColor,
            _ => new Color(1f, 1f, 1f, 1f)
        };

        private float GetWindSpeedForSeason(Season season) => season switch
        {
            Season.Spring => SpringWindSpeed,
            Season.Summer => SummerWindSpeed,
            Season.Fall => FallWindSpeed,
            Season.Winter => WinterWindSpeed,
            _ => 1.0f
        };

        /// <summary>
        /// Get the current season's foliage shader parameters as a color-encoded pair
        /// (useful for passing to shader material via SetShaderParameter).
        /// </summary>
        public Vector2 GetFoliageWindParams()
        {
            float windSpeed = GetWindSpeedForSeason(CurrentSeason);
            return new Vector2(windSpeed, FoliageWindStrength);
        }

        /// <summary>
        /// Get the interpolated current season color (during transitions).
        /// </summary>
        public Color GetCurrentSeasonColor() => _currentSeasonColor;

        public override void _ExitTree()
        {
            _seasonTransitionTween?.Kill();
        }
    }
}
