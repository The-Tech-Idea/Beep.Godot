using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Day/night cycle — the single source of time-of-day for the atmosphere system.
    ///
    /// Attach to a Node2D world root alongside an <see cref="AmbientController"/>. Runs a
    /// 24-hour clock, samples a four-key sky gradient (night → dawn → day → dusk), and
    /// contributes the resulting tint to the AmbientController. Optionally drives the
    /// viewport's clear colour so the horizon shifts too, not just the canvas.
    ///
    /// This used to be duplicated: WeatherSystemComponent had its own EnableDayNightCycle
    /// path. That copy was removed and its better logic (time in real hours, the sky
    /// gradient, the horizon clear-colour) folded in here, so there is now one authority.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DayNightCycleComponent : WorldComponent
    {
        public enum Phase { Dawn, Day, Dusk, Night }

        /// <summary>The tint layer key this component owns in the AmbientController.</summary>
        private const string ContributionKey = "day_night";

        [ExportGroup("Time")]
        /// <summary>Current time of day in hours [0..24). 8.0 = 8am. Settable from save data.</summary>
        [Export] public float TimeOfDay { get; set; } = 8.0f;

        /// <summary>Real seconds for a full 24-hour day.</summary>
        [Export] public float DayLengthSeconds { get; set; } = 120f;

        /// <summary>Whole in-game days elapsed since start — incremented each time
        /// <see cref="TimeOfDay"/> wraps past midnight. This is the derived day clock:
        /// SeasonalComponent reads it to advance seasons per in-game DAY, not per real second
        /// (the bug it used to have). A pure function of elapsed time, so it needs no clock type.</summary>
        public int DaysElapsed { get; private set; }

        [ExportGroup("Sky Key Colors")]
        // Sampled across the day and lerped between adjacent keys so dawn/dusk ramp smoothly.
        [Export] public Color NightSky { get; set; } = new(0.05f, 0.06f, 0.15f, 1f);   // 00:00
        [Export] public Color DawnSky  { get; set; } = new(0.85f, 0.55f, 0.40f, 1f);   // 06:00
        [Export] public Color DaySky   { get; set; } = new(1f,    0.98f, 0.92f, 1f);   // 12:00
        [Export] public Color DuskSky  { get; set; } = new(0.75f, 0.40f, 0.45f, 1f);   // 18:00

        [ExportGroup("Horizon")]
        /// <summary>Also drive RenderingServer's default clear colour from the sky gradient,
        /// so the area behind the world (the horizon) changes with the time of day.</summary>
        [Export] public bool DriveClearColor { get; set; } = true;

        [Signal] public delegate void TimeOfDayChangedEventHandler(float hours);
        [Signal] public delegate void PhaseChangedEventHandler(int phase);

        private AmbientController? _ambient;
        private Phase _currentPhase = Phase.Day;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            // Honor the genre's enable flag (mirrors WeatherSystemComponent). Without this the
            // clock + DriveClearColor ran even when day/night was disabled.
            if (Beep.GameBuilder.GameInfo.Instance is { } info) IsActive = info.EnableDayNightCycle;
            CallDeferred(nameof(Init));
        }

        private void Init()
        {
            _ambient = AmbientController.ForTree(this);
            Apply();  // seed the tint immediately rather than waiting a frame
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;

            float prev = TimeOfDay;
            TimeOfDay = (TimeOfDay + (float)delta * (24f / DayLengthSeconds)) % 24f;

            // Wrapped past midnight (new value fell below the old) → a whole in-game day passed.
            if (TimeOfDay < prev) DaysElapsed++;

            // Fire the hour signal only when crossing a whole-hour boundary, so label
            // listeners don't get spammed every frame.
            if ((int)prev != (int)TimeOfDay)
                EmitSignal(SignalName.TimeOfDayChanged, TimeOfDay);

            Apply();
        }

        /// <summary>Jump to a specific hour (save data, a "sleep" action, etc.).</summary>
        public void SetTimeOfDay(float hours)
        {
            TimeOfDay = ((hours % 24f) + 24f) % 24f;
            EmitSignal(SignalName.TimeOfDayChanged, TimeOfDay);
            Apply();
        }

        /// <summary>Time of day normalised to 0..1, for HUDs and forecast bars.</summary>
        public float TimeOfDayNormalized => TimeOfDay / 24f;

        private void Apply()
        {
            Color tint = SampleSky(TimeOfDay);

            _ambient?.SetContribution(ContributionKey, tint);

            if (DriveClearColor)
            {
                // Ease so it never snaps; the target moves every frame, so lerp at process
                // rate rather than via a tween.
                Color current = RenderingServer.GetDefaultClearColor();
                RenderingServer.SetDefaultClearColor(current.Lerp(tint, 0.05f));
            }

            UpdatePhase(TimeOfDay);
        }

        /// <summary>Four keyframes at 0/6/12/18h; the 18→24 segment wraps back to night.</summary>
        private Color SampleSky(float hours) => hours switch
        {
            < 6f  => Lerp(NightSky, DawnSky, hours / 6f),
            < 12f => Lerp(DawnSky,  DaySky,  (hours - 6f) / 6f),
            < 18f => Lerp(DaySky,   DuskSky, (hours - 12f) / 6f),
            _     => Lerp(DuskSky,  NightSky, (hours - 18f) / 6f)
        };

        private void UpdatePhase(float hours)
        {
            Phase p = hours switch
            {
                < 6f  => Phase.Night,
                < 10f => Phase.Dawn,
                < 18f => Phase.Day,
                < 21f => Phase.Dusk,
                _     => Phase.Night
            };
            if (p == _currentPhase) return;
            _currentPhase = p;
            EmitSignal(SignalName.PhaseChanged, (int)p);
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return new Color(Mathf.Lerp(a.R, b.R, t), Mathf.Lerp(a.G, b.G, t), Mathf.Lerp(a.B, b.B, t), 1f);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _ambient?.SetContribution(ContributionKey, null);
        }
    }
}
