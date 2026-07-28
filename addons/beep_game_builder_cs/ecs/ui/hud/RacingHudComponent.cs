using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Racing HUD: big Speed readout, plus Lap / Position / Lap-time.
    ///
    /// Driven by <see cref="RaceStateComponent"/>. All four were previously registered as
    /// <c>Placeholder(...)</c>, so every readout showed whatever text was typed into the scene
    /// and the lap clock never ran. Placeholder is now the FALLBACK for a scene with no race
    /// component, not the normal path.</summary>
    [Tool]
    [GlobalClass]
    public partial class RacingHudComponent : GenreHudComponent
    {
        [Export] public NodePath SpeedPath { get; set; } = "SpeedBox/SpeedLabel";
        [Export] public NodePath LapPath { get; set; } = "TopLeft/StatsVBox/LapLabel";
        [Export] public NodePath PositionPath { get; set; } = "TopLeft/StatsVBox/PositionLabel";
        [Export] public NodePath LapTimePath { get; set; } = "TopLeft/StatsVBox/LapTimeLabel";

        /// <summary>Optional toast host for best-lap and finish alerts.</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "racing";

        private RaceStateComponent? _race;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _speed, _lap, _position, _lapTime;

        protected override void Wire()
        {
            _race = FindInScene<RaceStateComponent>();

            if (_race == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts so the HUD
                // still functions, and say so once.
                Placeholder(SpeedPath, "speed");
                Placeholder(LapPath, "lap");
                Placeholder(PositionPath, "position");
                Placeholder(LapTimePath, "lap_time");
                return;
            }

            _speed = ResolveReadout(SpeedPath, "speed");
            _lap = ResolveReadout(LapPath, "lap");
            _position = ResolveReadout(PositionPath, "position");
            _lapTime = ResolveReadout(LapTimePath, "lap time");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _race.RaceChanged += OnRace;
            _race.NewBestLap += OnBestLap;
            _race.RaceFinished += OnFinished;
            OnRace();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_race != null && GodotObject.IsInstanceValid(_race))
            {
                _race.RaceChanged -= OnRace;
                _race.NewBestLap -= OnBestLap;
                _race.RaceFinished -= OnFinished;
            }
            _race = null;
        }

        private void OnRace()
        {
            if (_race == null) return;

            SetReadout(_speed, $"{Mathf.RoundToInt(_race.Speed)} {_race.SpeedUnit}",
                       _race.SpeedFraction);

            // Lap fills with progress through the RACE, not the lap, so the bar answers "how
            // much of this race is left" rather than repeating what the lap number already says.
            SetReadout(_lap, $"Lap {_race.Lap} / {_race.TotalLaps}", _race.RaceFraction);
            // Final lap is the cue every racing HUD flashes; finishing clears it.
            Tint(_lap, _race.Finished ? UiSurface.Role.Success
                 : _race.IsFinalLap ? UiSurface.Role.Warning
                 : null);

            SetReadout(_position, $"P{_race.Position} / {_race.Rivals.Count + 1}");
            Tint(_position, _race.Position == 1 ? UiSurface.Role.Success : null);

            SetReadout(_lapTime, _race.FormattedLapTime);
        }

        private void OnBestLap(float lapTime)
            => _alerts?.ShowToast($"Best lap  {RaceStateComponent.Format(lapTime)}",
                                  ToastNotificationComponent.ToastType.Success);

        private void OnFinished(float totalTime)
            => _alerts?.ShowToast($"Finished  {RaceStateComponent.Format(totalTime)}",
                                  ToastNotificationComponent.ToastType.Info);
    }
}
