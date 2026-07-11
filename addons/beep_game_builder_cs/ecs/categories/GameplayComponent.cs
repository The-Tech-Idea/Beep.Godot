using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for all gameplay components — health, attack, movement, inventory,
    /// AI, status effects, projectiles, pickups, interaction, damage, etc.
    ///
    /// Exists purely to organize components into a category folder in Godot's
    /// Add Node dialog. In the tree they appear as:
    ///   EntityComponent → GameplayComponent → (all gameplay components)
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class GameplayComponent : EntityComponent { }
}
