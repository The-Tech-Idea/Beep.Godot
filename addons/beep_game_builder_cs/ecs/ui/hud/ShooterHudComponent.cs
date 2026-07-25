using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Shooter HUD: Score/Level/Lives/Health bind live; Ammo and Wave are game-driven placeholders
    /// (bottom-right). A crosshair + genre theming live as sibling nodes in the scene.</summary>
    [Tool]
    [GlobalClass]
    public partial class ShooterHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopLeft/StatsVBox/ScoreLabel";
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath LivesPath { get; set; } = "TopLeft/StatsVBox/LivesLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath AmmoPath { get; set; } = "BottomRight/AmmoLabel";
        [Export] public NodePath WavePath { get; set; } = "BottomRight/WaveLabel";

        protected override string Genre => "shooter";

        protected override void Wire()
        {
            BindScore(ScorePath);
            BindLevel(LevelPath);
            BindLives(LivesPath);
            BindHealth(HealthPath);
            Placeholder(AmmoPath, "ammo");
            Placeholder(WavePath, "wave");
        }
    }
}
