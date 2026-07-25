using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Strategy HUD: top resource bar — Gold, Food, Wood, Units — plus a Turn readout. All are
    /// game-driven placeholders. A corner Minimap sits alongside in the scene.</summary>
    [Tool]
    [GlobalClass]
    public partial class StrategyHudComponent : GenreHudComponent
    {
        [Export] public NodePath GoldPath { get; set; } = "TopBar/Bar/GoldLabel";
        [Export] public NodePath FoodPath { get; set; } = "TopBar/Bar/FoodLabel";
        [Export] public NodePath WoodPath { get; set; } = "TopBar/Bar/WoodLabel";
        [Export] public NodePath UnitsPath { get; set; } = "TopBar/Bar/UnitsLabel";
        [Export] public NodePath TurnPath { get; set; } = "TurnLabel";

        protected override string Genre => "strategy";

        protected override void Wire()
        {
            Placeholder(GoldPath, "gold");
            Placeholder(FoodPath, "food");
            Placeholder(WoodPath, "wood");
            Placeholder(UnitsPath, "units");
            Placeholder(TurnPath, "turn");
        }
    }
}
