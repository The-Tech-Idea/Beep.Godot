using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// The four-stat HUD: Score, Level, Lives, Health, in a top-left stack.
    ///
    /// This exists because two genre HUDs had declared the same four NodePaths with the same four
    /// default values and the same four-line <c>Wire()</c>, character for character. Only their
    /// genre and one formatting choice actually differed, and those are what a subclass should be
    /// left holding.
    ///
    /// It is deliberately NOT part of <see cref="GenreHudComponent"/>. Racing has no lives and
    /// survival has no score; pushing these four onto every genre would hand most of them exports
    /// that resolve to nothing. A genre that wants this shape opts into it by deriving from here.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class StandardStatsHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopLeft/StatsVBox/ScoreLabel";
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath LivesPath { get; set; } = "TopLeft/StatsVBox/LivesLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";

        protected override void Wire()
        {
            BindScore(ScorePath);
            BindLevel(LevelPath);
            BindLives(LivesPath);
            BindHealth(HealthPath);
        }
    }
}
