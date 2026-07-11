using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Day/night cycle. Attach to a Node2D world root. Drives a CanvasModulate child
    /// through a full day cycle, adjusting ambient color from dawn → noon → dusk → night.
    /// Emits phase-change signals. Reads cycle length from the inspector (in-game seconds).
    /// Replaces day_night_cycle.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DayNightCycleComponent : WorldComponent
    {
        public enum Phase { Dawn, Day, Dusk, Night }

        [Export] public double DayLengthSeconds { get; set; } = 120.0;
        [Export] public Color DawnColor { get; set; } = new(1f, 0.7f, 0.5f, 1f);
        [Export] public Color DayColor { get; set; } = new(1f, 1f, 0.95f, 1f);
        [Export] public Color DuskColor { get; set; } = new(1f, 0.5f, 0.3f, 1f);
        [Export] public Color NightColor { get; set; } = new(0.3f, 0.35f, 0.5f, 1f);

        [Signal] public delegate void PhaseChangedEventHandler(int phase);

        private CanvasModulate? _ambient;
        private double _time;
        private Phase _currentPhase;

        public override void _Ready()
        {
            base._Ready();
            EnsureAmbient();
        }

        private void EnsureAmbient()
        {
            if (GetParent() is not Node parent) return;
            _ambient = parent.GetNodeOrNull<CanvasModulate>("DayNightAmbient");
            if (_ambient == null)
            {
                _ambient = new CanvasModulate { Name = "DayNightAmbient", Color = DayColor };
                parent.AddChild(_ambient);
                _ambient.Owner = parent;
            }
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _ambient == null) return;
            _time += delta;
            double t = (_time % DayLengthSeconds) / DayLengthSeconds; // 0..1

            // 4-phase interpolation: dawn(0-0.25) → day(0.25-0.5) → dusk(0.5-0.75) → night(0.75-1.0).
            Color c;
            Phase p;
            if (t < 0.25f)
            {
                c = DawnColor.Lerp(DayColor, (float)(t / 0.25));
                p = Phase.Dawn;
            }
            else if (t < 0.5f)
            {
                c = DayColor.Lerp(DuskColor, (float)((t - 0.25) / 0.25));
                p = Phase.Day;
            }
            else if (t < 0.75f)
            {
                c = DuskColor.Lerp(NightColor, (float)((t - 0.5) / 0.25));
                p = Phase.Dusk;
            }
            else
            {
                c = NightColor.Lerp(DawnColor, (float)((t - 0.75) / 0.25));
                p = Phase.Night;
            }

            _ambient.Color = c;
            if (p != _currentPhase)
            {
                _currentPhase = p;
                EmitSignal(SignalName.PhaseChanged, (int)p);
            }
        }

        /// <summary>Set the current time of day (0..1 where 0 = dawn, 0.5 = dusk).</summary>
        public void SetTimeOfDay(double t01) => _time = Mathf.Clamp(t01, 0, 1) * DayLengthSeconds;

        /// <summary>Current time of day as 0..1.</summary>
        public double TimeOfDay => (_time % DayLengthSeconds) / DayLengthSeconds;
    }
}
