using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for all world/environment components — weather, day/night,
    /// spawners, checkpoints, doors, moving platforms, parallax, wind field,
    /// particles, etc.
    ///
    /// Exists purely to organize components into a category folder in Godot's
    /// Add Node dialog. In the tree they appear as:
    ///   EntityComponent → WorldComponent → (all world/environment components)
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class WorldComponent : EntityComponent { }
}
