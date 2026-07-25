using Godot;

namespace Beep.ECS.UI
{
    /// <summary>City-builder HUD: top resource bar — Population, Budget, Power, Happiness, Date — all
    /// game-driven placeholders. A corner Minimap sits alongside in the scene.</summary>
    [Tool]
    [GlobalClass]
    public partial class CityBuilderHudComponent : GenreHudComponent
    {
        [Export] public NodePath PopulationPath { get; set; } = "TopBar/Bar/PopulationLabel";
        [Export] public NodePath BudgetPath { get; set; } = "TopBar/Bar/BudgetLabel";
        [Export] public NodePath PowerPath { get; set; } = "TopBar/Bar/PowerLabel";
        [Export] public NodePath HappinessPath { get; set; } = "TopBar/Bar/HappinessLabel";
        [Export] public NodePath DatePath { get; set; } = "TopBar/Bar/DateLabel";

        protected override string Genre => "citybuilder";

        protected override void Wire()
        {
            Placeholder(PopulationPath, "population");
            Placeholder(BudgetPath, "budget");
            Placeholder(PowerPath, "power");
            Placeholder(HappinessPath, "happiness");
            Placeholder(DatePath, "date");
        }
    }
}
