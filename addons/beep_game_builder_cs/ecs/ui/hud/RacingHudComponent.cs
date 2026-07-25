using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Racing HUD: big bottom-right Speed, plus Lap / Position / Lap-time. All are game-driven
    /// placeholders — real telemetry is the developer's (SetStat("speed", ...) each frame).</summary>
    [Tool]
    [GlobalClass]
    public partial class RacingHudComponent : GenreHudComponent
    {
        [Export] public NodePath SpeedPath { get; set; } = "SpeedBox/SpeedLabel";
        [Export] public NodePath LapPath { get; set; } = "TopLeft/StatsVBox/LapLabel";
        [Export] public NodePath PositionPath { get; set; } = "TopLeft/StatsVBox/PositionLabel";
        [Export] public NodePath LapTimePath { get; set; } = "TopLeft/StatsVBox/LapTimeLabel";

        protected override string Genre => "racing";

        protected override void Wire()
        {
            Placeholder(SpeedPath, "speed");
            Placeholder(LapPath, "lap");
            Placeholder(PositionPath, "position");
            Placeholder(LapTimePath, "lap_time");
        }
    }
}
