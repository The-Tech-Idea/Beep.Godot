using System;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Partial: the season-aware AutoCycle weather picker.
    ///
    /// This file used to also hold a full day/night cycle and its own Season enum, both of
    /// which duplicated standalone components. Those were removed:
    ///   • day/night  → <see cref="DayNightCycleComponent"/> (the better implementation)
    ///   • seasons    → <see cref="SeasonalComponent"/> (the single Season authority)
    ///
    /// What stays here is genuinely weather's own: choosing the next weather under AutoCycle,
    /// weighted by season and constrained to what's plausible for it. Season is READ from the
    /// SeasonalComponent in the scene rather than tracked here, so there is one source of truth.
    /// </summary>
    public partial class WeatherSystemComponent
    {
        [ExportGroup("Seasonal Cycling")]
        /// <summary>When true, AutoCycle only picks weather valid for the current season
        /// (read from the SeasonalComponent). No effect if there is no SeasonalComponent.</summary>
        [Export] public bool RestrictWeatherToSeason { get; set; } = true;

        private SeasonalComponent? _seasonal;

        /// <summary>The current season, read from the scene's SeasonalComponent. Falls back to
        /// Summer when none is present, so the picker still works in a weather-only scene.</summary>
        private SeasonalComponent.Season CurrentSeason
        {
            get
            {
                _seasonal ??= EntityComponent.FindComponent<SeasonalComponent>(GetTree()?.Root, true);
                return _seasonal?.CurrentSeason ?? SeasonalComponent.Season.Summer;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Season → weather availability + weights
        // ════════════════════════════════════════════════════════════════

        /// <summary>Is this weather type plausible for the current season? Used by the
        /// AutoCycle picker when RestrictWeatherToSeason is on.</summary>
        private bool IsWeatherValidForSeason(WeatherType w, SeasonalComponent.Season s) => (w, s) switch
        {
            (WeatherType.Snow, SeasonalComponent.Season.Summer) => false,
            (WeatherType.Hail, SeasonalComponent.Season.Summer) => false,
            (WeatherType.Heatwave, SeasonalComponent.Season.Winter) => false,
            (WeatherType.LeafFall, SeasonalComponent.Season.Spring) => false,
            (WeatherType.LeafFall, SeasonalComponent.Season.Winter) => false,
            (WeatherType.Sandstorm, SeasonalComponent.Season.Winter) => false,
            _ => true
        };

        /// <summary>Per-season weight table. Higher = more likely under AutoCycle. 0 means
        /// "allowed but rare". Inspired by mlavik1's weighted-picker.</summary>
        private float GetSeasonalWeight(WeatherType w) => (w, CurrentSeason) switch
        {
            (WeatherType.Clear, _) => 4f,
            (WeatherType.Cloudy, _) => 3f,
            (WeatherType.Rain, SeasonalComponent.Season.Spring) => 3f,
            (WeatherType.Rain, SeasonalComponent.Season.Summer) => 2f,
            (WeatherType.Rain, SeasonalComponent.Season.Fall) => 4f,
            (WeatherType.Rain, SeasonalComponent.Season.Winter) => 1f,
            (WeatherType.Snow, SeasonalComponent.Season.Winter) => 5f,
            (WeatherType.Snow, _) => 0.5f,
            (WeatherType.Storm, SeasonalComponent.Season.Summer) => 2f,
            (WeatherType.Storm, _) => 1f,
            (WeatherType.Fog, SeasonalComponent.Season.Fall) => 3f,
            (WeatherType.Fog, SeasonalComponent.Season.Spring) => 2f,
            (WeatherType.Fog, _) => 1f,
            (WeatherType.Sandstorm, SeasonalComponent.Season.Summer) => 2f,
            (WeatherType.Sandstorm, _) => 0.3f,
            (WeatherType.Hail, SeasonalComponent.Season.Winter) => 2f,
            (WeatherType.Hail, SeasonalComponent.Season.Fall) => 1f,
            (WeatherType.Hail, _) => 0.2f,
            (WeatherType.LeafFall, SeasonalComponent.Season.Fall) => 5f,
            (WeatherType.LeafFall, _) => 0f,
            (WeatherType.Heatwave, SeasonalComponent.Season.Summer) => 3f,
            (WeatherType.Heatwave, _) => 0f,
            _ => 1f
        };

        /// <summary>Pick the next weather for AutoCycle. Season-aware weighted random, never
        /// repeating the current weather. Falls back to sequential if all weights resolve to 0.</summary>
        private WeatherType PickWeightedWeather()
        {
            var season = CurrentSeason;
            var candidates = (WeatherType[])Enum.GetValues(typeof(WeatherType));
            float totalWeight = 0f;
            foreach (var w in candidates)
            {
                if (w == CurrentWeather) continue;
                if (RestrictWeatherToSeason && !IsWeatherValidForSeason(w, season)) continue;
                totalWeight += GetSeasonalWeight(w);
            }
            if (totalWeight <= 0f)
                return (WeatherType)(((int)CurrentWeather + 1) % candidates.Length);

            float roll = (float)GD.RandRange(0, totalWeight);
            float acc = 0f;
            foreach (var w in candidates)
            {
                if (w == CurrentWeather) continue;
                if (RestrictWeatherToSeason && !IsWeatherValidForSeason(w, season)) continue;
                acc += GetSeasonalWeight(w);
                if (roll <= acc) return w;
            }
            return WeatherType.Clear;
        }

        /// <summary>Min duration (seconds) for a weather type under AutoCycle.</summary>
        private double GetWeatherMinDuration(WeatherType w) => w switch
        {
            WeatherType.Storm => 20.0,
            WeatherType.Fog => 30.0,
            WeatherType.Sandstorm => 25.0,
            WeatherType.Clear => 40.0,
            _ => CycleInterval * 0.5
        };

        /// <summary>Max duration (seconds) for a weather type under AutoCycle.</summary>
        private double GetWeatherMaxDuration(WeatherType w) => w switch
        {
            WeatherType.Storm => 90.0,
            WeatherType.Fog => 120.0,
            WeatherType.Sandstorm => 80.0,
            WeatherType.Clear => 180.0,
            _ => CycleInterval * 1.5
        };
    }
}
