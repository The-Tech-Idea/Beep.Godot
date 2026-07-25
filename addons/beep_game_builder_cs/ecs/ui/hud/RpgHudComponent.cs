using Godot;

namespace Beep.ECS.UI
{
    /// <summary>RPG HUD: Level binds live from GameApp; Health, Mana and Quest are game-driven placeholders.
    /// A corner Minimap sits alongside in the scene.</summary>
    [Tool]
    [GlobalClass]
    public partial class RpgHudComponent : GenreHudComponent
    {
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath ManaPath { get; set; } = "TopLeft/StatsVBox/ManaLabel";
        [Export] public NodePath QuestPath { get; set; } = "QuestBox/QuestLabel";

        protected override string Genre => "rpg";

        protected override void Wire()
        {
            BindLevel(LevelPath);
            Placeholder(HealthPath, "health");
            Placeholder(ManaPath, "mana");
            Placeholder(QuestPath, "quest");
        }
    }
}
