using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Platformer HUD: minimalist top-left stack — Score, Level, Lives, Health. All bind live.</summary>
    [Tool]
    [GlobalClass]
    public partial class PlatformerHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopLeft/StatsVBox/ScoreLabel";
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath LivesPath { get; set; } = "TopLeft/StatsVBox/LivesLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";

        protected override string Genre => "platformer";

        protected override void Wire()
        {
            BindScore(ScorePath);
            BindLevel(LevelPath);
            BindLives(LivesPath);
            BindHealth(HealthPath);
        }
    }
}
