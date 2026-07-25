using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Top-down HUD: top-left Score/Level/Lives/Health (a corner Minimap node sits alongside in the scene).</summary>
    [Tool]
    [GlobalClass]
    public partial class TopDownHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopLeft/StatsVBox/ScoreLabel";
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath LivesPath { get; set; } = "TopLeft/StatsVBox/LivesLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";

        protected override string Genre => "topdown";

        protected override void Wire()
        {
            BindScore(ScorePath);
            BindLevel(LevelPath);
            BindLives(LivesPath);
            BindHealth(HealthPath);
        }
    }
}
