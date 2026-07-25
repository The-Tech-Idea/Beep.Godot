using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Puzzle HUD: centered Score binds live; Target and Moves are game-driven placeholders.</summary>
    [Tool]
    [GlobalClass]
    public partial class PuzzleHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopCenter/ScoreLabel";
        [Export] public NodePath TargetPath { get; set; } = "TopCenter/TargetLabel";
        [Export] public NodePath MovesPath { get; set; } = "TopCenter/MovesLabel";

        protected override string Genre => "puzzle";

        protected override void Wire()
        {
            BindScore(ScorePath);
            Placeholder(TargetPath, "target");
            Placeholder(MovesPath, "moves");
        }
    }
}
