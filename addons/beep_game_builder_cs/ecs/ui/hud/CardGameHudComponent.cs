using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Card-game HUD: player HP, Gold, Energy, and Deck/Discard counts — all game-driven
    /// placeholders (deck/economy logic is the developer's). Hand zone is a scene panel.</summary>
    [Tool]
    [GlobalClass]
    public partial class CardGameHudComponent : GenreHudComponent
    {
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath GoldPath { get; set; } = "TopRight/GoldLabel";
        [Export] public NodePath EnergyPath { get; set; } = "EnergyBox/EnergyLabel";
        [Export] public NodePath DeckPath { get; set; } = "BottomRight/DeckLabel";
        [Export] public NodePath DiscardPath { get; set; } = "BottomRight/DiscardLabel";

        protected override string Genre => "cardgame";

        protected override void Wire()
        {
            Placeholder(HealthPath, "health");
            Placeholder(GoldPath, "gold");
            Placeholder(EnergyPath, "energy");
            Placeholder(DeckPath, "deck");
            Placeholder(DiscardPath, "discard");
        }
    }
}
