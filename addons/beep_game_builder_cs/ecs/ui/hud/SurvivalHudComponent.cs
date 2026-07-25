using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Survival HUD: bottom-left vitals — Health, Hunger, Thirst, Stamina — all game-driven
    /// placeholders. Temperature/time come from the weather HUD; a corner Minimap sits alongside.</summary>
    [Tool]
    [GlobalClass]
    public partial class SurvivalHudComponent : GenreHudComponent
    {
        [Export] public NodePath HealthPath { get; set; } = "Vitals/HealthLabel";
        [Export] public NodePath HungerPath { get; set; } = "Vitals/HungerLabel";
        [Export] public NodePath ThirstPath { get; set; } = "Vitals/ThirstLabel";
        [Export] public NodePath StaminaPath { get; set; } = "Vitals/StaminaLabel";

        protected override string Genre => "survival";

        protected override void Wire()
        {
            Placeholder(HealthPath, "health");
            Placeholder(HungerPath, "hunger");
            Placeholder(ThirstPath, "thirst");
            Placeholder(StaminaPath, "stamina");
        }
    }
}
