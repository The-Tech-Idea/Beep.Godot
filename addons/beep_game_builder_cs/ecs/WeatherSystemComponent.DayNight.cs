using System;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Partial: day-night cycle + seasons.
    ///
    /// Why this is split out: the time-of-day engine is an independent
    /// subsystem (it would run on its own with weather = Clear) and the
    /// per-segment colour tables + season definitions make for a lot of
    /// constants that read cleaner away from the particle/lightning code.
    ///
    /// Design grounded in mlavik1/GodotWeatherSystem (day length, seasons
    /// gating available weather, weighted probabilities) and the wayline.io
    /// write-up (drive the sky clear color via RenderingServer so the
    /// horizon actually changes, not just the canvas tint).
    /// </summary>
    public partial class WeatherSystemComponent
    {
        public enum Season
        {
            Spring,
            Summer,
            Autumn,
            Winter
        }

        // ── Time-of-day exports ──
        [ExportGroup("Day / Night")]
        [Export] public bool EnableDayNightCycle { get; set; } = false;
        /// <summary>Current time of day in hours [0..24). Settable from save data.</summary>
        [Export] public float TimeOfDay { get; set; } = 8.0f;
        /// <summary>How many real seconds a full 24h day takes.</summary>
        [Export] public float DayLengthSeconds { get; set; } = 120f;

        // Four sky colours sampled across the day. Lerped between adjacent
        // keyframes so dawn/dusk ramp smoothly. Drives BOTH the canvas
        // modulate (light level) AND RenderingServer's clear color (horizon).
        [ExportSubgroup("Sky Key Colors")]
        [Export] public Color NightSky { get; set; } = new(0.05f, 0.06f, 0.15f, 1f);    // 00:00
        [Export] public Color DawnSky { get; set; } = new(0.85f, 0.55f, 0.40f, 1f);     // 06:00
        [Export] public Color DaySky { get; set; } = new(1f, 0.98f, 0.92f, 1f);         // 12:00
        [Export] public Color DuskSky { get; set; } = new(0.75f, 0.40f, 0.45f, 1f);     // 18:00

        [ExportGroup("Seasons")]
        [Export] public Season CurrentSeason { get; set; } = Season.Summer;
        /// <summary>When true, AutoCycle only picks weather valid for the season.</summary>
        [Export] public bool RestrictWeatherToSeason { get; set; } = true;

        // ── Day-night internal state ──
        private Color _dayNightTint = new(1, 1, 1, 1);   // current frame's tint from the sky gradient
        private Color _skyClearTarget = new(1, 1, 1, 1); // what we're easing the clear-color toward

        // ── Signals ──
        [Signal] public delegate void TimeOfDayChangedEventHandler(float hours);
        [Signal] public delegate void SeasonChangedEventHandler(int season);

        // ════════════════════════════════════════════════════════════════
        //  Per-frame day/night update — called from the main _Process
        // ════════════════════════════════════════════════════════════════

        private void ProcessDayNight(double delta)
        {
            if (!EnableDayNightCycle) return;

            float prev = TimeOfDay;
            TimeOfDay = (TimeOfDay + (float)delta * (24f / DayLengthSeconds)) % 24f;

            // Fire the hour signal only when we cross a whole-hour boundary
            // (avoids spamming every frame for listeners that update labels).
            if ((int)prev != (int)TimeOfDay)
                EmitSignal(SignalName.TimeOfDayChanged, TimeOfDay);

            // Recompute sky tint + clear-color target every frame (cheap: a few lerps).
            UpdateSkyColors();
        }

        /// <summary>Sample the 4-point sky gradient at the current TimeOfDay.</summary>
        private void UpdateSkyColors()
        {
            // Keyframes at 0, 6, 12, 18 hours; the 18→24 segment wraps to Night.
            _dayNightTint = TimeOfDay switch
            {
                < 6f  => LerpSky(NightSky, DawnSky, TimeOfDay / 6f),
                < 12f => LerpSky(DawnSky, DaySky,  (TimeOfDay - 6f) / 6f),
                < 18f => LerpSky(DaySky,  DuskSky, (TimeOfDay - 12f) / 6f),
                _     => LerpSky(DuskSky, NightSky, (TimeOfDay - 18f) / 6f)
            };
            _skyClearTarget = _dayNightTint;

            // Ease the actual clear color toward the target — instant snaps are
            // jarring during dawn/dusk; a frame lerp keeps it buttery.
            // (Done at process rate, not via tween, because the target moves.)
            Color current = RenderingServer.GetDefaultClearColor();
            Color eased = current.Lerp(_skyClearTarget, 0.05f);
            RenderingServer.SetDefaultClearColor(eased);
        }

        private static Color LerpSky(Color a, Color b, float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return new Color(
                Mathf.Lerp(a.R, b.R, t),
                Mathf.Lerp(a.G, b.G, t),
                Mathf.Lerp(a.B, b.B, t),
                1f);
        }

        /// <summary>
        /// The day/night contribution to the ambient tint. The main file
        /// multiplies this with the weather tint each frame so a storm at
        /// noon reads differently from a storm at midnight.
        /// </summary>
        private Color GetDayNightTint()
            => EnableDayNightCycle ? _dayNightTint : new Color(1, 1, 1, 1);

        // ════════════════════════════════════════════════════════════════
        //  Season → weather availability + weights
        // ════════════════════════════════════════════════════════════════

        /// <summary>Jump to a specific hour (e.g. from save data or a "sleep" action).</summary>
        public void SetTimeOfDay(float hours)
        {
            TimeOfDay = ((hours % 24f) + 24f) % 24f;
            if (EnableDayNightCycle) UpdateSkyColors();
            EmitSignal(SignalName.TimeOfDayChanged, TimeOfDay);
        }

        /// <summary>Advance the season and emit SeasonChanged.</summary>
        public void AdvanceSeason()
        {
            CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
            EmitSignal(SignalName.SeasonChanged, (int)CurrentSeason);
        }

        /// <summary>
        /// Is this weather type plausible for the current season? Used by
        /// the AutoCycle picker when RestrictWeatherToSeason is on.
        /// </summary>
        private bool IsWeatherValidForSeason(WeatherType w, Season s) => (w, s) switch
        {
            (WeatherType.Snow, Season.Summer) => false,
            (WeatherType.Hail, Season.Summer) => false,
            (WeatherType.Heatwave, Season.Winter) => false,
            (WeatherType.LeafFall, Season.Spring) => false,
            (WeatherType.LeafFall, Season.Winter) => false,
            (WeatherType.Sandstorm, Season.Winter) => false,
            _ => true
        };

        /// <summary>
        /// Per-season weight table. Higher = more likely under AutoCycle.
        /// 0 means "allowed but rare". Inspired by mlavik1's weighted-picker.
        /// </summary>
        private float GetSeasonalWeight(WeatherType w) => (w, CurrentSeason) switch
        {
            (WeatherType.Clear, _) => 4f,
            (WeatherType.Cloudy, _) => 3f,
            (WeatherType.Rain, Season.Spring) => 3f,
            (WeatherType.Rain, Season.Summer) => 2f,
            (WeatherType.Rain, Season.Autumn) => 4f,
            (WeatherType.Rain, Season.Winter) => 1f,
            (WeatherType.Snow, Season.Winter) => 5f,
            (WeatherType.Snow, _) => 0.5f,
            (WeatherType.Storm, Season.Summer) => 2f,
            (WeatherType.Storm, _) => 1f,
            (WeatherType.Fog, Season.Autumn) => 3f,
            (WeatherType.Fog, Season.Spring) => 2f,
            (WeatherType.Fog, _) => 1f,
            (WeatherType.Sandstorm, Season.Summer) => 2f,
            (WeatherType.Sandstorm, _) => 0.3f,
            (WeatherType.Hail, Season.Winter) => 2f,
            (WeatherType.Hail, Season.Autumn) => 1f,
            (WeatherType.Hail, _) => 0.2f,
            (WeatherType.LeafFall, Season.Autumn) => 5f,
            (WeatherType.LeafFall, _) => 0f,
            (WeatherType.Heatwave, Season.Summer) => 3f,
            (WeatherType.Heatwave, _) => 0f,
            _ => 1f
        };

        /// <summary>
        /// Pick the next weather for AutoCycle. Season-aware weighted random,
        /// never repeating the current weather. Falls back to sequential if all
        /// weights somehow resolve to 0.
        /// </summary>
        private WeatherType PickWeightedWeather()
        {
            var candidates = (WeatherType[])Enum.GetValues(typeof(WeatherType));
            float totalWeight = 0f;
            foreach (var w in candidates)
            {
                if (w == CurrentWeather) continue;
                if (RestrictWeatherToSeason && !IsWeatherValidForSeason(w, CurrentSeason)) continue;
                totalWeight += GetSeasonalWeight(w);
            }
            if (totalWeight <= 0f)
                return (WeatherType)(((int)CurrentWeather + 1) % candidates.Length);

            float roll = (float)GD.RandRange(0, totalWeight);
            float acc = 0f;
            foreach (var w in candidates)
            {
                if (w == CurrentWeather) continue;
                if (RestrictWeatherToSeason && !IsWeatherValidForSeason(w, CurrentSeason)) continue;
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
